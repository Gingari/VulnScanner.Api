using Api.Models;

namespace Analyzer.Scanners;

public interface ICodeScanner
{
    Task<ScanResult> ScanAsync(
        string repositoryPath,
        CancellationToken cancellationToken);
}