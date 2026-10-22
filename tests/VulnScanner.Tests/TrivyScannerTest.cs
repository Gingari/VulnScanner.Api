using Analyzer.Processes;
using Analyzer.Scanners;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace VulnScanner.Tests;

public sealed class TrivyScannerTests
{
    [Fact]
    public async Task EmptyReport_ReturnsZeroFindings()
    {
        var runner = new FakeProcessRunner();
        var scanner = CreateScanner(runner);

        var result = await scanner.ScanAsync(
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.Equal(0, result.TotalFindings);
        Assert.Empty(result.Findings);

        AssertReportDeleted(runner);
    }

    [Theory]
    [InlineData("CRITICAL", "Critical")]
    [InlineData("HIGH", "High")]
    [InlineData("MEDIUM", "Medium")]
    [InlineData("LOW", "Low")]
    [InlineData("UNKNOWN", "Unknown")]
    public async Task Vulnerability_IsMappedCorrectly(
        string sourceSeverity,
        string expectedSeverity)
    {
        var runner = new FakeProcessRunner
        {
            Report = $$"""
                {
                  "Results": [
                    {
                      "Target": "packages.lock.json",
                      "Vulnerabilities": [
                        {
                          "VulnerabilityID": "CVE-TEST-0001",
                          "PkgName": "Example.Package",
                          "InstalledVersion": "1.0.0",
                          "FixedVersion": "1.0.1",
                          "Severity": "{{sourceSeverity}}",
                          "Title": "Test vulnerability"
                        }
                      ]
                    }
                  ]
                }
                """
        };

        var scanner = CreateScanner(runner);

        var result = await scanner.ScanAsync(
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.Equal(1, result.TotalFindings);

        var finding = Assert.Single(result.Findings);

        Assert.Equal("trivy", finding.Scanner);
        Assert.Equal(
            "CVE-TEST-0001: Example.Package",
            finding.Name);

        Assert.Equal(expectedSeverity, finding.Severity);
        Assert.Equal("packages.lock.json", finding.FilePath);

        Assert.NotNull(finding.Description);
        Assert.Contains("1.0.0", finding.Description);
        Assert.Contains("1.0.1", finding.Description);

        AssertReportDeleted(runner);
    }

    [Fact]
    public async Task ProcessError_ThrowsAndDeletesReport()
    {
        var runner = new FakeProcessRunner
        {
            ExitCode = 1
        };

        var scanner = CreateScanner(runner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scanner.ScanAsync(
                Path.GetTempPath(),
                CancellationToken.None));

        AssertReportDeleted(runner);
    }

    [Fact]
    public async Task MissingReport_ThrowsAndDeletesDirectory()
    {
        var runner = new FakeProcessRunner
        {
            WriteReport = false
        };

        var scanner = CreateScanner(runner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scanner.ScanAsync(
                Path.GetTempPath(),
                CancellationToken.None));

        AssertReportDeleted(runner);
    }

    [Fact]
    public async Task Cancellation_IsPropagatedAndDeletesReport()
    {
        using var source = new CancellationTokenSource();

        var runner = new FakeProcessRunner
        {
            OnRun = source.Cancel
        };

        var scanner = CreateScanner(runner);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(
                Path.GetTempPath(),
                source.Token));

        AssertReportDeleted(runner);
    }

    private static TrivyScanner CreateScanner(
        IProcessRunner runner)
    {
        return new TrivyScanner(
            runner,
            NullLogger<TrivyScanner>.Instance);
    }

    private static void AssertReportDeleted(
        FakeProcessRunner runner)
    {
        Assert.NotNull(runner.ReportDirectory);
        Assert.False(Directory.Exists(runner.ReportDirectory));
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public int ExitCode { get; init; }

        public string Report { get; init; } =
            """{"Results": []}""";

        public bool WriteReport { get; init; } = true;

        public Action? OnRun { get; init; }

        public string? ReportDirectory { get; private set; }

        public async Task<ProcessResult> RunAsync(
            string executable,
            IEnumerable<string> arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            Assert.Equal("trivy", executable);

            var args = arguments.ToArray();
            var outputIndex = Array.IndexOf(args, "--output");

            Assert.True(outputIndex >= 0);
            Assert.True(outputIndex + 1 < args.Length);

            var reportPath = args[outputIndex + 1];

            ReportDirectory = Path.GetDirectoryName(reportPath);

            OnRun?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            if (WriteReport)
            {
                await File.WriteAllTextAsync(
                    reportPath,
                    Report,
                    cancellationToken);
            }

            return new ProcessResult(
                ExitCode,
                string.Empty,
                string.Empty,
                false);
        }
    }
}