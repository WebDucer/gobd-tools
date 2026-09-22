using System.Text;
using GoBd.Validation.Cli;
using GoBd.Validation.Dtd;

namespace GoBd.Validation.Tests.Cli;

/// <summary>Result of driving the CLI in-process.</summary>
/// <param name="ExitCode">The code the process would return.</param>
/// <param name="Output">Everything written to standard output.</param>
/// <param name="Error">Everything written to standard error.</param>
public sealed record CliResult(int ExitCode, string Output, string Error);

/// <summary>Drives the CLI exactly as a terminal would, with its streams captured.</summary>
public static class CliHarness
{
    /// <summary>Runs the CLI with the given arguments.</summary>
    public static CliResult Run(params string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CliRunner.Run(arguments, output, error);
        return new CliResult(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Runs the CLI with environment variables temporarily set.</summary>
    public static CliResult RunWithEnvironment(IReadOnlyDictionary<string, string?> environment, params string[] arguments)
    {
        var restore = environment.Keys.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);
        try
        {
            foreach (var (name, value) in environment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            return Run(arguments);
        }
        finally
        {
            foreach (var (name, value) in restore)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    /// <summary>Creates a temporary export folder from an index.xml body plus extra files.</summary>
    public static string CreateExport(string indexXml, bool includeDtd = true, params (string Name, string Content)[] files)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gobd-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "index.xml"), indexXml, Encoding.UTF8);
        if (includeDtd)
        {
            File.WriteAllBytes(Path.Combine(directory, CanonicalDtd.FileName), CanonicalDtd.Bytes.ToArray());
        }

        foreach (var (name, content) in files)
        {
            var path = Path.Combine(directory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return directory;
    }
}
