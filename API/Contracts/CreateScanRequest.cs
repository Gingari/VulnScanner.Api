using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public sealed class CreateScanRequest
{
    [Required]
    [Url]
    public required string RepositoryUrl { get; init; }
}