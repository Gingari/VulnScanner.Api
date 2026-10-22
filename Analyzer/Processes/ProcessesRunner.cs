using System.Diagnostics;
using System.Text;

namespace Analyzer.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    private const int MaxOutputCharacters = 64_000;

    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (environment is not null)
        {
            foreach (var variable in environment)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }
        using var process = new Process
        {
            StartInfo = startInfo
        };

        using var timeoutSource = new CancellationTokenSource(timeout);

        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Не удалось запустить {executable}.");
        }

        process.StandardInput.Close();

        var outputTask = ReadOutputAsync(process.StandardOutput);
        var errorTask = ReadOutputAsync(process.StandardError);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);

            var output = await outputTask;
            var error = await errorTask;

            linkedSource.Token.ThrowIfCancellationRequested();

            return new ProcessResult(
                process.ExitCode,
                output.Text,
                error.Text,
                output.Truncated || error.Truncated);
        }
        catch (OperationCanceledException)
            when (linkedSource.IsCancellationRequested)
        {
            if (timeoutSource.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Превышено время выполнения {executable}.");
            }

            throw;
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }

            await process.WaitForExitAsync();

            await Task.WhenAll(outputTask, errorTask);
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadOutputAsync(
        StreamReader reader)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;

        int count;

        while ((count = await reader.ReadAsync(
                   buffer, 0, buffer.Length)) > 0)
        {
            var remaining = MaxOutputCharacters - result.Length;
            var charactersToSave = Math.Min(remaining, count);

            if (charactersToSave > 0)
            {
                result.Append(buffer, 0, charactersToSave);
            }

            if (charactersToSave < count)
            {
                truncated = true;
            }
        }

        return (result.ToString(), truncated);
    }
}