using Api.Contracts;
using Api.Models;
using Api.Repositories;
using Api.Exceptions;
using Analyzer.Git;

namespace Api.Services;

public sealed class ScanService
{
    private readonly ScanRepository _repository;
    private readonly ScanCancelRegistry _cancelRegistry;
    private readonly ScanQueue _queue;
    private readonly ReposUrlValidator _urlValidator;

    public ScanService(
        ScanRepository repository,
        ScanCancelRegistry cancelRegistry,
        ScanQueue queue,
        ReposUrlValidator urlValidator)
    {
        _repository = repository;
        _cancelRegistry = cancelRegistry;
        _queue = queue;
        _urlValidator = urlValidator;
    }

    public ScanJob Create(CreateScanRequest request)
    {
        if (!_urlValidator.IsAllowed(request.RepositoryUrl))
            throw new InvalidRepositoryUrlException();
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
        try
        {
            _cancelRegistry.Create(scanJob.Id);

            if (!_queue.Enqueue(scanJob.Id))
                throw new ScanQueueFullException();
        }
        catch
        {
            _cancelRegistry.Remove(scanJob.Id);
            _repository.Remove(scanJob.Id);
            throw;
        }
        return scanJob;
    }

    public ScanJob? GetById(Guid id)
    {
        return _repository.GetById(id);
    }

    public CancelScanResponse? Cancel(Guid id)
    {
        var scanJob = _repository.GetById(id);

        if (scanJob is null)
            return null;

        lock (scanJob.SyncRoot)
        {
            if (scanJob.Status is ScanStatus.Completed
                or ScanStatus.Failed
                or ScanStatus.Cancelled
                or ScanStatus.TimedOut)
            {
                throw new ScanConflictException(
                    "Завершённое сканирование нельзя отменить.");
            }
            if (scanJob.Status == ScanStatus.CancellationRequested)
            {
                return new CancelScanResponse(
                    id,
                    scanJob.LastStatusChangedAt,
                    true);
            }
            var now = DateTimeOffset.UtcNow;
            scanJob.Status = scanJob.Status == ScanStatus.Queued
                ? ScanStatus.Cancelled
                : ScanStatus.CancellationRequested;

            scanJob.LastStatusChangedAt = now;
            _cancelRegistry.Cancel(id);
            return new CancelScanResponse(id, now, true);
        }
    }
}