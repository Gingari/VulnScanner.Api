using Api.Models;

namespace Analyzer;

public interface IScanOrchestrator
{
    Task<ScanResult> ScanAsync(
        Guid scanId,
        string repositoryUrl,
        CancellationToken cancellationToken);
}