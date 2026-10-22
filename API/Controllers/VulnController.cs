using Microsoft.AspNetCore.Mvc;
using Api.Services;
using Api.Contracts;
using Api.Models;

namespace Api.Controllers;

[ApiController]
[Route("v1/vulnerabilities")]
public class VulnController : ControllerBase
{
    private readonly VulnService _service;

    public VulnController(VulnService service)
    {
        _service = service;
    }

    /// <summary>
    /// Добавить уязвимость.
    /// </summary>
    /// <remarks>
    /// Необходимо указать название и критичность:
    /// Low, Medium, High или Critical.
    ///
    /// Идентификатор и дата создания назначаются автоматически.
    /// В ответе возвращается ID, а в заголовке Location —
    /// ссылка для получения полной записи.
    ///
    /// Пример запроса:
    ///
    ///     {
    ///       "name": "SQL Injection",
    ///       "severity": "Critical"
    ///     }
    ///
    /// </remarks>
    /// <param name="request">Название и критичность уязвимости.</param>
    /// <response code="201">Уязвимость создана. Возвращён её ID.</response>
    /// <response code="400">Не указаны обязательные поля или значения некорректны.</response>
    /// <response code="500">Внутренняя ошибка сервера.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError)]
    public IActionResult CreateVuln(
        [FromBody] CreateVulnRequest request)
    {
        var vuln = _service.Create(request);

        return CreatedAtAction(
            nameof(GetById),
            new { id = vuln.Id },
            new { id = vuln.Id });
    }

    /// <summary>
    /// Получить уязвимость по ID.
    /// </summary>
    /// <remarks>
    /// Возвращает идентификатор, название, критичность
    /// и дату создания записи.
    ///
    /// Данные хранятся в памяти и удаляются
    /// после перезапуска приложения.
    /// </remarks>
    /// <param name="id">Идентификатор уязвимости в формате GUID.</param>
    /// <response code="200">Уязвимость найдена.</response>
    /// <response code="404">Уязвимость не найдена или ID не соответствует формату маршрута.</response>
    /// <response code="500">Внутренняя ошибка сервера.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Vuln), StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError)]
    public IActionResult GetById(Guid id)
    {
        var vuln = _service.GetById(id);

        return vuln is null
            ? NotFound()
            : Ok(vuln);
    }
}