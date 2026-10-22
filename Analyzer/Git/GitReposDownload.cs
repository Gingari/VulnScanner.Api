using Analyzer.Processes;
using Api.Options;
using Microsoft.Extensions.Options;

namespace Analyzer.Git;

public sealed class GitReposDownloader : IGitReposDownloader
{
    private readonly ProcessRunner _processRunner;
    private readonly ReposUrlValidator _urlValidator;
    private readonly ReposOptions _options;
    private readonly ILogger<GitReposDownloader> _logger;

    public GitReposDownloader(
        ProcessRunner processRunner,
        ReposUrlValidator urlValidator,
        IOptions<ReposOptions> options,
        ILogger<GitReposDownloader> logger)
    {
        _processRunner = processRunner;
        _urlValidator = urlValidator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> DownloadAsync(
        Guid scanId,
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        if (!_urlValidator.IsAllowed(repositoryUrl))
        {
            throw new InvalidOperationException(
                "Адрес репозитория не прошёл проверку.");
        }

        var rootDirectory =
            Path.GetFullPath(_options.TemporaryDirectory);

        Directory.CreateDirectory(rootDirectory);

        var repositoryDirectory = Path.Combine(
            rootDirectory,
            $"{scanId:N}-{Guid.NewGuid():N}");

        var nullFile = OperatingSystem.IsWindows()
            ? "NUL"
            : "/dev/null";

        var environment = new Dictionary<string, string>
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GCM_INTERACTIVE"] = "never",
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = nullFile,
            ["GIT_CONFIG_SYSTEM"] = nullFile,

            ["GIT_CONFIG_COUNT"] = "0",
            ["GIT_CONFIG_PARAMETERS"] = ""
        };

        try
        {
            _logger.LogInformation(
                "Downloading repository for scan {ScanId}",
                scanId);

            var result = await _processRunner.RunAsync(
                executable: "git",
                arguments:
                [
                    "-c", "http.followRedirects=false",
                    "-c", "credential.helper=",
                    "-c", "protocol.allow=never",
                    "-c", "protocol.https.allow=always",
                    "-c", "init.templateDir=",

                    "clone",
                    "--depth", "1",
                    "--single-branch",
                    "--no-recurse-submodules",
                    "--",
                    repositoryUrl,
                    repositoryDirectory
                ],
                workingDirectory: rootDirectory,
                timeout: TimeSpan.FromSeconds(
                    _options.CloneTimeoutSeconds),
                cancellationToken: cancellationToken,
                environment: environment);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Не удалось скачать репозиторий. " +
                    "Проверьте, что он существует и является публичным.");
            }

            return repositoryDirectory;
        }
        catch
        {
            Cleanup(repositoryDirectory);
            throw;
        }
    }

    public void Cleanup(string repositoryDirectory)
    {
        try
        {
            if (!Directory.Exists(repositoryDirectory))
                return;

            foreach (var file in Directory.EnumerateFiles(
                         repositoryDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(
                repositoryDirectory,
                recursive: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to remove temporary directory {Directory}",
                repositoryDirectory);
        }
    }
}