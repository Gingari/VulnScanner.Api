using Microsoft.AspNetCore.Mvc;
using Api.Services;
using Api.Contracts;

namespace Api.Controllers;

[ApiController]
[Route("v1/vulnerabilities")]
public class VulnerabilitiesController : ControllerBase
{
    private readonly VulnService _service;

    public VulnerabilitiesController(VulnService service)
    {
        _service = service;
    }

    [HttpPost]
    public IActionResult CreateVuln(CreateVulnRequest request)
    {
        var vulnerability = _service.Create(request);
        return CreatedAtAction(nameof(GetById),
            new { id = vulnerability.Id },
            vulnerability);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var vulnerability = _service.GetById(id);

        return vulnerability is null
            ? NotFound()
            : Ok(vulnerability);
    }

}