using Api.Repositories;
using Api.Models;
using Api.Contracts;

namespace Api.Services;

public sealed class VulnService
{
    private readonly VulnRepository _repository;

    public VulnService(VulnRepository repository)
    {
        _repository = repository;
    }  
    public Vuln Create(CreateVulnRequest request)
    {
        var vuln = new Vuln
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Severity = request.Severity,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _repository.Add(vuln);
        return vuln;
    }
    
    public Vuln GetById(Guid id)
    {
        var vuln = _repository.GetById(id);
        if (vuln == null)
        {
            throw new KeyNotFoundException($"Vulnerability with ID {id} not found.");
        }
        return vuln;
    }
}