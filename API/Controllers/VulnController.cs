using Microsoft.AspNetCore.Mvc;
using Api.Services;
using Api.Contracts;

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

    [HttpPost]
    public IActionResult CreateVuln(CreateVulnRequest request)
    {
        var vuln = _service.Create(request);
        return CreatedAtAction(nameof(GetById),
            new { id = vuln.Id }, new { id = vuln.Id });
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var vuln = _service.GetById(id);

        return vuln is null
            ? NotFound()
            : Ok(vuln);
    }

}