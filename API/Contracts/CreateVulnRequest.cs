using System.ComponentModel.DataAnnotations;
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
        [Required]
        [StringLength(200)]
        public required string Name { get; init; }

        [EnumDataType(typeof(SeverityLevel))]
        public required SeverityLevel Severity { get; init; }
    }
}