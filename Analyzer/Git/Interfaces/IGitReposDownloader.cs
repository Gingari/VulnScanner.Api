namespace Analyzer.Git;

public interface IGitReposDownloader
{
    Task<string> DownloadAsync(
        Guid scanId,
        string repositoryUrl,
        CancellationToken cancellationToken);

    void Cleanup(string repositoryDirectory);
}