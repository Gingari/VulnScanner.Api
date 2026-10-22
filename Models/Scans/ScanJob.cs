namespace Api.Models;
using System.Text.Json.Serialization;
public sealed class ScanJob
{
    public Guid Id { get; init; }

    public required string RepositoryUrl { get; init; }

    public ScanStatus Status { get; set; } = ScanStatus.Queued;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastStatusChangedAt { get; set; }

    public ScanResult? Result { get; set; }

    public string? Error { get; set; }
    public bool CancellationRequested { get; internal set; }
    [JsonIgnore]
    public object SyncRoot { get; } = new();
}