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

    [HttpPost]
    public IActionResult CreateScan([FromBody] CreateScanRequest request)
    {
        var scan = _service.Create(request);

        return AcceptedAtAction(
            nameof(GetById),
            new { id = scan.Id },
            scan);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var scan = _service.GetById(id);

        return scan is null
            ? NotFound()
            : Ok(scan);
    }
    [HttpPost("{id:guid}/cancel")]
    public IActionResult Cancel(Guid id)
    {
        var result = _service.Cancel(id);

        if (result is null)
            return NotFound();

        if (!result.CancellationRequested)
            return Conflict(result);

        return Ok(result);
    }
}