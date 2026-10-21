namespace Api.Models;

public sealed class ScanResult
{
    public int TotalFindings { get; init; }

    public List<FindingVuln> Findings { get; init; } = [];
}