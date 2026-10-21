using System.Threading.Channels;

namespace Api.Services;

public sealed class ScanQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>();

    public bool Enqueue(Guid scanId)
    {
        return _channel.Writer.TryWrite(scanId);
    } 
    public IAsyncEnumerable<Guid> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}