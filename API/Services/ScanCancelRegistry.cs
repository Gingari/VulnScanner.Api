using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace Api.Services;

public sealed class ScanCancelRegistry: IDisposable
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public CancellationTokenSource Create(Guid scanId)
    {
        var source = new CancellationTokenSource();

        if (!_sources.TryAdd(scanId, source))
        {
            source.Dispose();
            throw new InvalidOperationException(
                $"Cancellation source for scan {scanId} already exists.");
        }

        return source;
    }

    public CancellationToken? GetToken(Guid scanId)
    {
        return _sources.TryGetValue(scanId, out var source)
            ? source.Token
            : null;
    }

    public bool Cancel(Guid scanId)
    {
        if (!_sources.TryGetValue(scanId, out var source))
            return false;

        source.Cancel();
        return true;
    }

    public void Remove(Guid scanId)
    {
        if (_sources.TryRemove(scanId, out var source))
            source.Dispose();
    }

    public void Dispose()
    {
        foreach (var source in _sources.Values)
            source.Dispose();

        _sources.Clear();
    }
}