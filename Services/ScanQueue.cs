using System.Threading.Channels;
using Microsoft.Extensions.Options;

using Api.Options;

namespace Api.Services;

public sealed class ScanQueue
{
    private readonly Channel<Guid> _channel;

    public ScanQueue(IOptions<ScanningOptions> options)
    {
        _channel = Channel.CreateBounded<Guid>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
    }

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