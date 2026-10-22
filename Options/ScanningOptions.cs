namespace Api.Options;

public sealed class ScanningOptions
{
    public int MaxParallelScans { get; set; } = 2;

    public int QueueCapacity { get; set; } = 10;
}