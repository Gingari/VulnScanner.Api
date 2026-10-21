namespace Api.Contracts
{
    public enum SeverityLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public sealed class CreateVulnRequest
    {
        public required string Name { get; init; }

        public required SeverityLevel Severity { get; init; }
    }
}