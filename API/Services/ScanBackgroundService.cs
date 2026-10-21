using Api.Models;
using Api.Repositories;
using Api.Scanners;

namespace Api.Services;

public sealed class ScanBackgroundService : BackgroundService
{
    private readonly ScanQueue _queue;
    private readonly ScanRepository _repository;
    private readonly ScanCancelRegistry _cancelRegistry;
    private readonly ICodeScanner _scanner;
    private readonly ILogger<ScanBackgroundService> _logger;

    public ScanBackgroundService(
        ScanQueue queue,
        ScanRepository repository,
        ScanCancelRegistry cancelRegistry,
        ICodeScanner scanner,
        ILogger<ScanBackgroundService> logger)
    {
        _queue = queue;
        _repository = repository;
        _cancelRegistry = cancelRegistry;
        _scanner = scanner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (
            var scanId in _queue.ReadAllAsync(stoppingToken))
        {
            await ProcessScanAsync(scanId, stoppingToken);
        }
    }

    private async Task ProcessScanAsync(
        Guid scanId,
        CancellationToken stoppingToken)
    {
        var scanJob = _repository.GetById(scanId);
        var scanToken = _cancelRegistry.GetToken(scanId);

        if (scanJob is null || scanToken is null)
            return;

        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                scanToken.Value);

        try
        {
            // Задачу могли отменить, пока она ждала в очереди.
            if (scanJob.Status == ScanStatus.Cancelled ||
                linkedSource.IsCancellationRequested)
            {
                scanJob.Status = ScanStatus.Cancelled;
                return;
            }

            scanJob.Status = ScanStatus.Running;
            scanJob.LastStatusChangedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Scan {ScanId} started for {RepositoryUrl}",
                scanId,
                scanJob.RepositoryUrl);

            scanJob.Result = await _scanner.ScanAsync(
                scanJob.RepositoryUrl,
                linkedSource.Token);

            scanJob.Status = ScanStatus.Completed;

            _logger.LogInformation(
                "Scan {ScanId} completed",
                scanId);
        }
        catch (OperationCanceledException)
        {
            scanJob.Status = ScanStatus.Cancelled;

            _logger.LogInformation(
                "Scan {ScanId} cancelled",
                scanId);
        }
        catch (Exception exception)
        {
            scanJob.Status = ScanStatus.Failed;
            scanJob.Error = exception.Message;

            _logger.LogError(
                exception,
                "Scan {ScanId} failed",
                scanId);
        }
        finally
        {
            scanJob.LastStatusChangedAt = DateTimeOffset.UtcNow;
            _cancelRegistry.Remove(scanId);
        }
    }
}