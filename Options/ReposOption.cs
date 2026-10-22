namespace Api.Options;

public sealed class ReposOptions
{
    public string[] AllowedHosts { get; set; } = [];
    public string TemporaryDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "vuln-scanner");
    public int CloneTimeoutSeconds { get; set; } = 120;
}