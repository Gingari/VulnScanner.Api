using System.Collections.Concurrent;
using Api.Models;

namespace Api.Repositories;

public sealed class VulnRepository
{
    private readonly ConcurrentDictionary<Guid, Vuln> _items = new();

    public void Add(Vuln vuln)
    {
        _items[vuln.Id] = vuln;
    }

    public Vuln? GetById(Guid id)
    {
        return _items.TryGetValue(id, out var item) ? item : null;
    }
}