using System.Diagnostics;

using Analyzer.Git;
using Api.Models;
using Analyzer .Scanners;

namespace Analyzer;

public sealed class ScanOrchestrator : IScanOrchestrator
{
    private readonly IGitReposDownloader _downloader;
    private readonly IEnumerable<ICodeScanner> _scanners;
    private readonly ILogger<ScanOrchestrator> _logger;

    public ScanOrchestrator(
        IGitReposDownloader downloader,
        IEnumerable<ICodeScanner> scanners,
        ILogger<ScanOrchestrator> logger)
    {
        _downloader = downloader;
        _scanners = scanners;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(
        Guid scanId,
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        string? directory = null;

        try
        {
            directory = await _downloader.DownloadAsync(
                scanId,
                repositoryUrl,
                cancellationToken);

            var findings = new List<FindingVuln>();

            foreach (var scanner in _scanners)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scannerName = scanner.GetType().Name;
                var stopwatch = Stopwatch.StartNew();

                _logger.LogInformation(
                    "Scan {ScanId}: analyzer {Analyzer} started",
                    scanId,
                    scannerName);

                try
                {
                    var result = await scanner.ScanAsync(
                        directory,
                        cancellationToken);

                    stopwatch.Stop();

                    findings.AddRange(result.Findings);

                    _logger.LogInformation(
                        "Scan {ScanId}: analyzer {Analyzer} completed. " +
                        "Findings: {FindingsCount}. Duration: {ElapsedMs} ms",
                        scanId,
                        scannerName,
                        result.Findings.Count,
                        stopwatch.ElapsedMilliseconds);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Scan {ScanId}: analyzer {Analyzer} cancelled after {ElapsedMs} ms",
                        scanId,
                        scannerName,
                        stopwatch.ElapsedMilliseconds);

                    throw;
                }
                catch (Exception)
                {
                    _logger.LogWarning(
                        "Scan {ScanId}: analyzer {Analyzer} failed after {ElapsedMs} ms",
                        scanId,
                        scannerName,
                        stopwatch.ElapsedMilliseconds);

                    throw;
                }
            }
                        return new ScanResult
            {
                TotalFindings = findings.Count,
                Findings = findings
            };
        }
        finally
        {
            if (directory is not null)
                _downloader.Cleanup(directory);
        }
    }
}