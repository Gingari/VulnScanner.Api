using Api.Models;

namespace Api.Scanners;

public sealed class TestCodeScanner : ICodeScanner
{
    public async Task<ScanResult> ScanAsync(
        string repositoryUrl,
        CancellationToken cancellationToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(10),
            cancellationToken);

        return new ScanResult
        {
            TotalFindings = 0,
            Findings = []
        };
    }
}