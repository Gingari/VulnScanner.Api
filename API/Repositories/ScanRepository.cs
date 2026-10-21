using System.Collections.Concurrent;
using Api.Models;

namespace Api.Repositories;

public sealed class ScanRepository
{
    private readonly ConcurrentDictionary<Guid, ScanJob> _items = new();

    public void Add(ScanJob scan)
    {
        _items[scan.Id] = scan;
    }

    public ScanJob? GetById(Guid id)
    {
        return _items.TryGetValue(id, out var item) ? item : null;
    }
}