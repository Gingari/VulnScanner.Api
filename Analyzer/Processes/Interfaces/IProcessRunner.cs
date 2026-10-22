namespace Analyzer.Processes;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null);
}