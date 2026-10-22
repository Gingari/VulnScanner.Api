using System.Text.Json;
using Analyzer.Processes;
using Api.Models;

namespace Analyzer.Scanners;

public sealed class TrivyScanner : ICodeScanner
{
    private const long MaxReportBytes = 20 * 1024 * 1024;

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<TrivyScanner> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public TrivyScanner(
        IProcessRunner processRunner,
        ILogger<TrivyScanner> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        string? workDirectory = null;

        try
        {
            workDirectory = Path.Combine(
                Path.GetTempPath(),
                "vuln-scanner-reports",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(workDirectory);

            var reportPath = Path.Combine(workDirectory, "report.json");
            var configPath = Path.Combine(workDirectory, "trivy.yaml");
            var ignorePath = Path.Combine(workDirectory, ".trivyignore");

            await File.WriteAllTextAsync(
                configPath,
                "{}",
                cancellationToken);

            await File.WriteAllTextAsync(
                ignorePath,
                string.Empty,
                cancellationToken);

            var processResult = await _processRunner.RunAsync(
                executable: "trivy",
                arguments:
                [
                    "fs",

                    "--config", configPath,
                    "--ignorefile", ignorePath,

                    "--scanners", "vuln",
                    "--format", "json",
                    "--output", reportPath,

                    "--exit-code", "0",

                    "--timeout", "5m",
                    "--no-progress",

                    Path.GetFullPath(repositoryPath)
                ],
                workingDirectory: workDirectory,
                timeout: TimeSpan.FromMinutes(6),
                cancellationToken: cancellationToken);

            if (processResult.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Trivy завершился с ошибкой. " +
                    $"Код: {processResult.ExitCode}.");
            }

            if (!File.Exists(reportPath))
            {
                throw new InvalidOperationException(
                    "Trivy не создал отчёт.");
            }

            if (new FileInfo(reportPath).Length > MaxReportBytes)
            {
                throw new InvalidOperationException(
                    "Отчёт Trivy превысил допустимый размер.");
            }

            await using var stream = File.OpenRead(reportPath);

            var report = await JsonSerializer.DeserializeAsync<TrivyReport>(
                stream,
                cancellationToken: cancellationToken);

            if (report is null)
            {
                throw new InvalidOperationException(
                    "Trivy вернул некорректный отчёт.");
            }

            var findings = new List<FindingVuln>();

            foreach (var target in report.Results ?? [])
            {
                foreach (var vulnerability in target.Vulnerabilities ?? [])
                {
                    findings.Add(new FindingVuln
                    {
                        Scanner = "trivy",

                        Name =
                            $"{vulnerability.VulnerabilityID}: " +
                            vulnerability.PkgName,

                        Severity = MapSeverity(vulnerability.Severity),

                        FilePath = target.Target,

                        Description =
                            $"{vulnerability.Title ?? "Уязвимая зависимость"}. " +
                            $"Установлена: {vulnerability.InstalledVersion}. " +
                            $"Исправлена: " +
                            $"{vulnerability.FixedVersion ?? "не указано"}."
                    });
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
            try
            {
                if (workDirectory is not null &&
                    Directory.Exists(workDirectory))
                {
                    Directory.Delete(workDirectory, recursive: true);
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Не удалось удалить временный отчёт Trivy");
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private static string MapSeverity(string? severity)
    {
        return severity?.ToUpperInvariant() switch
        {
            "CRITICAL" => "Critical",
            "HIGH" => "High",
            "MEDIUM" => "Medium",
            "LOW" => "Low",
            _ => "Unknown"
        };
    }

    public sealed class TrivyReport
    {
        public List<TrivyTarget>? Results { get; init; }
    }

    public sealed class TrivyTarget
    {
        public string? Target { get; init; }

        public List<TrivyVulnerability>? Vulnerabilities { get; init; }
    }

    public sealed class TrivyVulnerability
    {
        public string VulnerabilityID { get; init; } = string.Empty;

        public string PkgName { get; init; } = string.Empty;

        public string? InstalledVersion { get; init; }

        public string? FixedVersion { get; init; }

        public string? Severity { get; init; }

        public string? Title { get; init; }
    }
}