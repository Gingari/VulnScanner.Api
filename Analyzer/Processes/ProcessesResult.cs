namespace Analyzer.Processes;

public sealed record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool OutputTruncated);