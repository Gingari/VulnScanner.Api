namespace Api.Contracts;

public sealed record CancelScanResponse(
    Guid Id,
    DateTimeOffset RequestedAt,
    bool CancellationRequested);