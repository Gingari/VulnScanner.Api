using Microsoft.Extensions.Options;

using Api.Models;
using Api.Repositories;
using Api.Options;
using Analyzer;

namespace Api.Services;

public sealed class ScanBackgroundService : BackgroundService
{
    private readonly ScanQueue _queue;
    private readonly ScanRepository _repository;
    private readonly ScanCancelRegistry _cancelRegistry;
    private readonly int _maxParallelScans;
    private readonly IScanOrchestrator _orchestrator;
    private readonly ILogger<ScanBackgroundService> _logger;

    public ScanBackgroundService(ScanQueue queue, ScanRepository repository, ScanCancelRegistry cancelRegistry, IScanOrchestrator orchestrator, ILogger<ScanBackgroundService> logger, IOptions<ScanningOptions> options){
        _queue = queue;
        _repository = repository;
        _cancelRegistry = cancelRegistry;
        _orchestrator = orchestrator;
        _logger = logger;
        _maxParallelScans = options.Value.MaxParallelScans;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, _maxParallelScans)
            .Select(_ => WorkerAsync(stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task WorkerAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (
                var scanId in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessScanAsync(scanId, stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Нормальная остановка приложения.
        }
    }

    private async Task ProcessScanAsync(Guid scanId, CancellationToken stoppingToken)
    {
        var scanJob = _repository.GetById(scanId);

        if (scanJob is null)
            return;

        CancellationTokenSource? linkedSource = null;

        try
        {
            lock (scanJob.SyncRoot)
            {
                if (scanJob.Status != ScanStatus.Queued)
                    return;

                var scanToken = _cancelRegistry.GetToken(scanId)
                    ?? throw new InvalidOperationException(
                        "Не найден токен отмены сканирования.");

                linkedSource =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        stoppingToken,
                        scanToken);

                linkedSource.Token.ThrowIfCancellationRequested();

                SetStatus(scanJob, ScanStatus.Running);
            }

            _logger.LogInformation(
                "Scan {ScanId} started",
                scanId);

            var result = await _orchestrator.ScanAsync(
                scanJob.Id,
                scanJob.RepositoryUrl,
                linkedSource.Token);

            lock (scanJob.SyncRoot)
            {
                linkedSource.Token.ThrowIfCancellationRequested();

                scanJob.Result = result;
                SetStatus(scanJob, ScanStatus.Completed);
            }

            _logger.LogInformation(
                "Scan {ScanId} completed",
                scanId);
        }
        catch (OperationCanceledException)
            when (linkedSource?.IsCancellationRequested == true)
        {
            lock (scanJob.SyncRoot)
            {
                SetStatus(scanJob, ScanStatus.Cancelled);
            }

            _logger.LogInformation(
                "Scan {ScanId} cancelled",
                scanId);
        }
        catch (Exception exception)
        {
            lock (scanJob.SyncRoot)
            {
                if (linkedSource?.IsCancellationRequested == true)
                {
                    SetStatus(scanJob, ScanStatus.Cancelled);
                }
                else
                {
                    scanJob.Error = "Не удалось выполнить сканирование.";
                    SetStatus(scanJob, ScanStatus.Failed);
                }
            }

            _logger.LogError(
                exception,
                "Scan {ScanId} failed",
                scanId);
        }
        finally
        {
            lock (scanJob.SyncRoot)
            {
                _cancelRegistry.Remove(scanId);
            }

            linkedSource?.Dispose();
        }
    }

    private static void SetStatus(ScanJob scanJob, ScanStatus status)
    {
        scanJob.Status = status;
        scanJob.LastStatusChangedAt = DateTimeOffset.UtcNow;
    }
}