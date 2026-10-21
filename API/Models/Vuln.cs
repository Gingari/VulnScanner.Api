using Api.Contracts;

namespace Api.Models;

public sealed class Vuln
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required SeverityLevel Severity { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}