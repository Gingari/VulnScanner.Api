using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("v1/scans")]
public sealed class ScansController : ControllerBase
{
    private readonly ScanService _service;

    public ScansController(ScanService service)
    {
        _service = service;
    }
    /// <summary>
    /// Запустить сканирование репозитория.
    /// </summary>
    /// <remarks>
    /// Принимает HTTPS-ссылку на публичный репозиторий разрешённого хоста.
    /// Задача помещается в очередь, её ID возвращается сразу.
    /// Статус и результат доступны через GET /v1/scans/{id}.
    ///
    /// Пример запроса:
    ///
    ///     {
    ///       "repositoryUrl": "https://github.com/Gingari/VulnScanner.Api"
    ///     }
    ///
    /// </remarks>
    /// <response code="202">Задача принята. Возвращаются ID и заголовок Location.</response>
    /// <response code="400">Некорректный или запрещённый адрес.</response>
    /// <response code="429">Очередь заполнена.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public IActionResult CreateScan([FromBody] CreateScanRequest request)
    {
        var scan = _service.Create(request);

        return AcceptedAtAction(
            nameof(GetById),
            new { id = scan.Id },
            new { id = scan.Id });
    }
    /// <summary>
    /// Получить статус и результат сканирования.
    /// </summary>
    /// <param name="id">Идентификатор задачи.</param>
    /// <remarks>
    /// Результат доступен после успешного завершения.
    /// Для существующей задачи возвращается 200, даже если её статус Failed.
    /// </remarks>
    /// <response code="200">Текущее состояние задачи.</response>
    /// <response code="404">Задача не найдена.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Api.Models.ScanJob), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetById(Guid id)
    {
        var scan = _service.GetById(id);

        return scan is null
            ? NotFound()
            : Ok(scan);
    }
    
    /// <summary>
    /// Запросить отмену сканирования.
    /// </summary>
    /// <param name="id">Идентификатор задачи.</param>
    /// <remarks>
    /// Можно отменить ожидающую или выполняющуюся задачу.
    /// Успешный ответ подтверждает принятие запроса отмены.
    /// Фактическая остановка подтверждается статусом Cancelled.
    /// </remarks>
    /// <response code="200">Возвращены ID, время и подтверждение запроса отмены.</response>
    /// <response code="404">Задача не найдена.</response>
    /// <response code="409">Задача уже находится в конечном состоянии.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Cancel(Guid id)
    {
        var response = _service.Cancel(id);

        return response is null
            ? NotFound()
            : Ok(response);
    }
}