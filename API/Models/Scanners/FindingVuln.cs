namespace Api.Models;

public sealed class FindingVuln
{
    public required string Scanner { get; init; }

    public required string Name { get; init; }

    public required string Severity { get; init; }

    public string? FilePath { get; init; }

    public string? Description { get; init; }
}