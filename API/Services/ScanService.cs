using Api.Contracts;
using Api.Models;
using Api.Repositories;

namespace Api.Services;

public sealed class ScanService
{
private readonly ScanQueue _queue;

public ScanService(
    ScanRepository repository,
    ScanCancelRegistry cancelRegistry,
    ScanQueue queue)
{
    _repository = repository;
    _cancelRegistry = cancelRegistry;
    _queue = queue;
}
    public ScanJob Create(CreateScanRequest request)
    {
        var now = DateTimeOffset.UtcNow;

        var scanJob = new ScanJob
        {
            Id = Guid.NewGuid(),
            RepositoryUrl = request.RepositoryUrl,
            Status = ScanStatus.Queued,
            CreatedAt = now,
            LastStatusChangedAt = now
        };

        _repository.Add(scanJob);
        _cancelRegistry.Create(scanJob.Id);
        _queue.Enqueue(scanJob.Id);

        return scanJob;
    }

    public ScanJob? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public ScanJob? Cancel(Guid id)
    {
        var scanJob = _repository.GetById(id);

        if (scanJob is null)
            return null;

        if (scanJob.Status is ScanStatus.Completed
            or ScanStatus.Failed
            or ScanStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Завершённое сканирование нельзя отменить.");
        }

        scanJob.Status = scanJob.Status == ScanStatus.Queued
            ? ScanStatus.Cancelled
            : ScanStatus.CancellationRequested;

        scanJob.LastStatusChangedAt = DateTimeOffset.UtcNow;

        _cancelRegistry.Cancel(scanJob.Id);

        return scanJob;
    }
}