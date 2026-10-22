using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Exceptions;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            KeyNotFoundException =>
                (404, "Объект не найден"),
            ScanConflictException =>
                (409, "Конфликт состояния"),
            ScanQueueFullException =>
                (429, "Очередь заполнена"),
            InvalidRepositoryUrlException =>
                (400, "Недопустимый адрес репозитория"),
            _ =>
                (500, "Внутренняя ошибка сервера")
        };

        if (status == 500)
        {
            logger.LogError(
                exception,
                "Ошибка обработки запроса. TraceId: {TraceId}",
                httpContext.TraceIdentifier);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500
                ? "Произошла непредвиденная ошибка."
                : exception.Message,
            Instance = httpContext.Request.Path.Value
        };

        problem.Extensions["traceId"] =
            httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}