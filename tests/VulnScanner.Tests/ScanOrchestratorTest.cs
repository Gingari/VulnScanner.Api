using Analyzer;
using Analyzer.Git;
using Analyzer.Scanners;
using Api.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace VulnScanner.Tests;

public sealed class ScanOrchestratorTests
{
    [Fact]
    public async Task Results_AreCombined_AndRepositoryIsDeleted()
    {
        var downloader = new FakeDownloader();

        var scanners = new ICodeScanner[]
        {
            new FakeScanner((_, _) =>
                Task.FromResult(CreateResult("gitleaks", 1))),

            new FakeScanner((_, _) =>
                Task.FromResult(CreateResult("trivy", 2)))
        };

        var orchestrator = CreateOrchestrator(downloader, scanners);

        var result = await orchestrator.ScanAsync(
            Guid.NewGuid(),
            "https://github.com/example/test.git",
            CancellationToken.None);

        Assert.Equal(3, result.TotalFindings);
        Assert.Equal(3, result.Findings.Count);

        Assert.Single(
            result.Findings,
            x => x.Scanner == "gitleaks");

        Assert.Equal(
            2,
            result.Findings.Count(x => x.Scanner == "trivy"));

        Assert.Equal(1, downloader.DownloadCount);
        Assert.Equal(1, downloader.CleanupCount);

        Assert.All(scanners.Cast<FakeScanner>(), scanner =>
            Assert.Equal(
                downloader.RepositoryDirectory,
                scanner.ReceivedPath));

        Assert.False(
            Directory.Exists(downloader.RepositoryDirectory));
    }

    [Fact]
    public async Task ScannerFailure_IsPropagated_AndRepositoryIsDeleted()
    {
        var downloader = new FakeDownloader();

        var scanner = new FakeScanner((_, _) =>
            Task.FromException<ScanResult>(
                new InvalidOperationException("Test scanner failure")));

        var orchestrator = CreateOrchestrator(
            downloader,
            [scanner]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ScanAsync(
                Guid.NewGuid(),
                "https://github.com/example/test.git",
                CancellationToken.None));

        Assert.Equal(1, downloader.CleanupCount);

        Assert.False(
            Directory.Exists(downloader.RepositoryDirectory));
    }

    [Fact]
    public async Task Cancellation_IsPropagated_AndRepositoryIsDeleted()
    {
        using var source = new CancellationTokenSource();

        var downloader = new FakeDownloader();

        var scanner = new FakeScanner((_, token) =>
        {
            Assert.Equal(source.Token, token);

            source.Cancel();
            token.ThrowIfCancellationRequested();

            return Task.FromResult(new ScanResult());
        });

        var orchestrator = CreateOrchestrator(
            downloader,
            [scanner]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => orchestrator.ScanAsync(
                Guid.NewGuid(),
                "https://github.com/example/test.git",
                source.Token));

        Assert.Equal(1, downloader.CleanupCount);

        Assert.False(
            Directory.Exists(downloader.RepositoryDirectory));
    }

    private static ScanOrchestrator CreateOrchestrator(
        IGitReposDownloader downloader,
        IEnumerable<ICodeScanner> scanners)
    {
        return new ScanOrchestrator(
            downloader,
            scanners,
            NullLogger<ScanOrchestrator>.Instance);
    }

    private static ScanResult CreateResult(
        string scannerName,
        int count)
    {
        var findings = Enumerable.Range(1, count)
            .Select(index => new FindingVuln
            {
                Scanner = scannerName,
                Name = $"Test finding {index}",
                Severity = "High"
            })
            .ToList();

        return new ScanResult
        {
            TotalFindings = findings.Count,
            Findings = findings
        };
    }

    private sealed class FakeScanner(
        Func<string, CancellationToken, Task<ScanResult>> execute)
        : ICodeScanner
    {
        public string? ReceivedPath { get; private set; }

        public Task<ScanResult> ScanAsync(
            string repositoryPath,
            CancellationToken cancellationToken)
        {
            ReceivedPath = repositoryPath;

            return execute(repositoryPath, cancellationToken);
        }
    }

    private sealed class FakeDownloader : IGitReposDownloader
    {
        public string RepositoryDirectory { get; } =
            Path.Combine(
                Path.GetTempPath(),
                "vuln-scanner-tests",
                Guid.NewGuid().ToString("N"));

        public int DownloadCount { get; private set; }

        public int CleanupCount { get; private set; }

        public Task<string> DownloadAsync(
            Guid scanId,
            string repositoryUrl,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DownloadCount++;
            Directory.CreateDirectory(RepositoryDirectory);

            return Task.FromResult(RepositoryDirectory);
        }

        public void Cleanup(string repositoryDirectory)
        {
            CleanupCount++;

            Directory.Delete(
                repositoryDirectory,
                recursive: true);
        }
    }
}