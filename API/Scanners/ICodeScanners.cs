using Api.Models;

namespace Api.Scanners;

public interface ICodeScanner
{
    Task<ScanResult> ScanAsync(
        string repositoryUrl,
        CancellationToken cancellationToken);
}