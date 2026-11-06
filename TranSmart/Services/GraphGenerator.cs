using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TranSmart.Services;

public interface IGraphGenerator
{
    Task GenerateAsync(string outputPath, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class PythonScraperGraphGenerator : IGraphGenerator
{
    private static readonly TimeSpan InterpreterProbeTimeout = TimeSpan.FromSeconds(4);
    private static readonly Regex PythonThreeVersion = new(@"\bPython\s+3\.", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly string _scriptPath;

    public PythonScraperGraphGenerator(string? scriptPath = null)
    {
        _scriptPath = scriptPath ?? Path.Combine(AppContext.BaseDirectory, "scraper.py");
    }

    public async Task GenerateAsync(
        string outputPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_scriptPath))
        {
            throw new FileNotFoundException($"The graph scraper was not found at '{_scriptPath}'.", _scriptPath);
        }

        var python = await FindPythonAsync(cancellationToken);
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

        var arguments = python.PrefixArguments.ToList();
        arguments.Add(_scriptPath);
        arguments.Add("--output");
        arguments.Add(fullOutputPath);

        var result = await ExecuteAsync(python.Executable, arguments, progress, cancellationToken);
        if (result.ExitCode != 0)
        {
            var details = result.StandardErrorTail.Count > 0
                ? string.Join(Environment.NewLine, result.StandardErrorTail)
                : result.StandardOutput.Trim();
            throw new InvalidOperationException(
                $"Graph generation failed (exit code {result.ExitCode}). " +
                (string.IsNullOrWhiteSpace(details) ? "Check the Python and OSMnx installation." : details));
        }

        if (!File.Exists(fullOutputPath))
        {
            throw new InvalidOperationException("The scraper finished without creating the graph file.");
        }
    }

    private static async Task<PythonCommand> FindPythonAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        foreach (var candidate in GetPythonCandidates())
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(InterpreterProbeTimeout);
            try
            {
                var result = await ExecuteAsync(candidate.Executable, candidate.PrefixArguments.Append("--version"), null, timeout.Token);
                if (result.ExitCode == 0 && PythonThreeVersion.IsMatch(result.StandardOutput))
                {
                    return candidate;
                }

                failures.Add($"{candidate.Executable} did not report a usable Python 3 version.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add($"{candidate.Executable} did not respond to the version check.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add($"{candidate.Executable}: {exception.Message}");
            }
        }

        throw new InvalidOperationException(
            "Python 3 could not be found. Install Python 3, add it to PATH, or set TRANSMART_PYTHON to its executable. " +
            "OSMnx must also be installed in that Python environment. " + string.Join(" ", failures));
    }

    private static IEnumerable<PythonCommand> GetPythonCandidates()
    {
        var configuredPython = Environment.GetEnvironmentVariable("TRANSMART_PYTHON");
        if (!string.IsNullOrWhiteSpace(configuredPython))
        {
            yield return new PythonCommand(configuredPython, []);
        }

        yield return new PythonCommand("py", ["-3"]);
        yield return new PythonCommand("python", []);
        yield return new PythonCommand("python3", []);
    }

    private static async Task<ProcessResult> ExecuteAsync(
        string executable,
        IEnumerable<string> arguments,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        var standardOutput = new StringBuilder();
        var standardErrorTail = new Queue<string>();
        var outputTask = ReadLinesAsync(process.StandardOutput, standardOutput, progress, null);
        var errorTask = ReadLinesAsync(process.StandardError, null, null, standardErrorTail);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
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

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(outputTask, errorTask);
            throw;
        }

        await Task.WhenAll(outputTask, errorTask);
        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardErrorTail.ToArray());
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder? capture,
        IProgress<string>? progress,
        Queue<string>? tail)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            capture?.AppendLine(line);
            progress?.Report(line);
            if (tail is null)
            {
                continue;
            }

            tail.Enqueue(line);
            while (tail.Count > 20)
            {
                tail.Dequeue();
            }
        }
    }

    private sealed record PythonCommand(string Executable, IReadOnlyList<string> PrefixArguments);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, IReadOnlyList<string> StandardErrorTail);
}
