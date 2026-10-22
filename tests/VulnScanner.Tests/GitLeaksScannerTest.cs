using System.Text.Json;
using Analyzer.Processes;
using Analyzer.Scanners;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace VulnScanner.Tests;

public sealed class GitleaksScannerTests
{
    [Fact]
    public async Task NoFindings_ReturnsEmptyResult_AndDeletesReport()
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

    [Fact]
    public async Task Finding_IsMapped_WithoutExposingSecret()
    {
        var runner = new FakeProcessRunner
        {
            ExitCode = 10,
            Report = """
                [
                  {
                    "RuleID": "generic-api-key",
                    "File": "settings.txt",
                    "StartLine": 3,
                    "Secret": "FAKE_TEST_SECRET_NOT_A_REAL_KEY",
                    "Match": "token=FAKE_TEST_SECRET_NOT_A_REAL_KEY"
                  }
                ]
                """
        };

        var scanner = CreateScanner(runner);

        var result = await scanner.ScanAsync(
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.Equal(1, result.TotalFindings);

        var finding = Assert.Single(result.Findings);

        Assert.Equal("gitleaks", finding.Scanner);
        Assert.Equal("generic-api-key", finding.Name);
        Assert.Equal("settings.txt", finding.FilePath);
        Assert.Equal("High", finding.Severity);

        Assert.DoesNotContain(
            "FAKE_TEST_SECRET_NOT_A_REAL_KEY",
            JsonSerializer.Serialize(result));

        AssertReportDeleted(runner);
    }

    [Fact]
    public async Task ProcessError_Throws_AndDeletesReport()
    {
        var runner = new FakeProcessRunner
        {
            ExitCode = 2
        };

        var scanner = CreateScanner(runner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scanner.ScanAsync(
                Path.GetTempPath(),
                CancellationToken.None));

        AssertReportDeleted(runner);
    }

    [Fact]
    public async Task Cancellation_IsPropagated_AndDeletesReport()
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

    private static GitleaksScanner CreateScanner(
        IProcessRunner runner)
    {
        return new GitleaksScanner(
            runner,
            NullLogger<GitleaksScanner>.Instance);
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

        public string Report { get; init; } = "[]";

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
            var args = arguments.ToArray();

            var reportFlagIndex = Array.IndexOf(
                args,
                "--report-path");

            Assert.True(reportFlagIndex >= 0);

            var reportPath = args[reportFlagIndex + 1];

            ReportDirectory = Path.GetDirectoryName(reportPath);

            OnRun?.Invoke();

            cancellationToken.ThrowIfCancellationRequested();

            await File.WriteAllTextAsync(
                reportPath,
                Report,
                cancellationToken);

            return new ProcessResult(
                ExitCode,
                string.Empty,
                string.Empty,
                false);
        }
    }
}