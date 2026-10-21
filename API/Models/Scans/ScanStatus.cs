namespace Api.Models;

public enum ScanStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    CancellationRequested,
    Cancelled,
    TimedOut
}