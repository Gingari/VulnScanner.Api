using System.Text.Json;
using Analyzer.Processes;
using Api.Models;

namespace Analyzer.Scanners;

public sealed class GitleaksScanner : ICodeScanner
{
    private const int FindingsExitCode = 10;
    private const long MaxReportBytes = 10 * 1024 * 1024;

    private readonly IProcessRunner _processRunner;

    private readonly ILogger<GitleaksScanner> _logger;

    public GitleaksScanner(
        IProcessRunner processRunner,
        ILogger<GitleaksScanner> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "vuln-scanner-reports",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workDirectory);

        var reportPath = Path.Combine(workDirectory, "report.json");
        var configPath = Path.Combine(workDirectory, "gitleaks.toml");
        var ignorePath = Path.Combine(workDirectory, ".gitleaksignore");

        try
        {
            await File.WriteAllTextAsync(
                configPath,
                "[extend]\nuseDefault = true\n",
                cancellationToken);

            await File.WriteAllTextAsync(
                ignorePath,
                string.Empty,
                cancellationToken);

            var processResult = await _processRunner.RunAsync(
                executable: "gitleaks",
                arguments:
                [
                    "dir",
                    Path.GetFullPath(repositoryPath),

                    "--config", configPath,
                    "--gitleaks-ignore-path", ignorePath,
                    "--ignore-gitleaks-allow",

                    "--report-format", "json",
                    "--report-path", reportPath,
                    "--exit-code", FindingsExitCode.ToString(),

                    "--redact=100",
                    "--no-banner"
                ],
                workingDirectory: workDirectory,
                timeout: TimeSpan.FromMinutes(2),
                cancellationToken: cancellationToken);

            if (processResult.ExitCode != 0 &&
                processResult.ExitCode != FindingsExitCode)
            {
                throw new InvalidOperationException(
                    $"Gitleaks завершился с ошибкой. " +
                    $"Код: {processResult.ExitCode}.");
            }

            if (!File.Exists(reportPath))
            {
                throw new InvalidOperationException(
                    "Gitleaks не создал отчёт.");
            }

            if (new FileInfo(reportPath).Length > MaxReportBytes)
            {
                throw new InvalidOperationException(
                    "Отчёт Gitleaks превысил допустимый размер.");
            }

            await using var stream = File.OpenRead(reportPath);

            var report = await JsonSerializer.DeserializeAsync<
                List<GitleaksFinding>>(
                    stream,
                    cancellationToken: cancellationToken);

            if (report is null)
            {
                throw new InvalidOperationException(
                    "Gitleaks вернул некорректный отчёт.");
            }

            var findings = report.Select(item => new FindingVuln
            {
                Scanner = "gitleaks",
                Name = item.RuleID,

                Severity = "High",

                FilePath = ToRelativePath(repositoryPath, item.File),

                Description =
                    $"Обнаружен потенциальный секрет. " +
                    $"Правило: {item.RuleID}. Строка: {item.StartLine}."
            }).ToList();

            return new ScanResult
            {
                TotalFindings = findings.Count,
                Findings = findings
            };
        }
        finally
        {
            try
            {
                Directory.Delete(workDirectory, recursive: true);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Не удалось удалить временный отчёт Gitleaks");
            }
        }
    }

    private static string ToRelativePath(
        string repositoryPath,
        string filePath)
    {
        return Path.IsPathRooted(filePath)
            ? Path.GetRelativePath(repositoryPath, filePath)
            : filePath;
    }

    public sealed class GitleaksFinding
    {
        public string RuleID { get; init; } = string.Empty;

        public string File { get; init; } = string.Empty;

        public int StartLine { get; init; }
    }
}