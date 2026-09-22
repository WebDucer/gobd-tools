using System.Reflection;

namespace GoBd.Validation.Tests.Documentation;

/// <summary>
/// Holds the README's usage block to the command's actual help.
/// </summary>
/// <remarks>
/// A usage block that has drifted from the tool is worse than none: it is the first thing a
/// person reads and the last thing anyone remembers to update. The comparison is line by line
/// against the help the command actually prints.
/// </remarks>
public sealed class ReadmeUsageTests
{
    private static string Readme { get; } = File.ReadAllText(Locate());

    private static string Locate()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        for (var candidate = new DirectoryInfo(directory); candidate is not null; candidate = candidate.Parent)
        {
            var path = Path.Combine(candidate.FullName, "README.md");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("README.md was not found.");
    }

    /// <summary>The usage block, taken from the fence that starts with the command line itself.</summary>
    private static IReadOnlyList<string> UsageBlock()
    {
        var lines = Readme.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith("gobd-validate <export>", StringComparison.Ordinal));
        start.ShouldBeGreaterThan(0, "the README has no usage block");

        var end = Array.FindIndex(lines, start, line => line.StartsWith("```", StringComparison.Ordinal));
        end.ShouldBeGreaterThan(start, "the usage block is not closed");

        return [.. lines[start..end].Where(line => line.Trim().Length > 0)];
    }

    [Fact]
    public void TheUsageBlockIsNotEmpty() =>
        // Guard on the guard: an empty block would make the comparison below vacuous.
        UsageBlock().Count.ShouldBeGreaterThan(10);

    [Fact]
    public void EveryLineOfTheUsageBlockAppearsInTheHelp()
    {
        var help = Cli.CliHarness.Run("--help").Output.Replace("\r\n", "\n", StringComparison.Ordinal);
        var missing = UsageBlock().Where(line => !help.Contains(line, StringComparison.Ordinal)).ToArray();

        missing.ShouldBeEmpty($"the README documents lines the command does not print: {string.Join(" / ", missing)}");
    }

    [Fact]
    public void EveryOptionTheCommandAcceptsIsInTheUsageBlock()
    {
        var block = string.Join("\n", UsageBlock());
        var options = new[]
        {
            "--format", "--output", "--language", "--strict", "--contents", "--max-findings",
            "--help", "--version", "--license", "--notice", "--third-party-notices",
        };
        var missing = options.Where(option => !block.Contains(option, StringComparison.Ordinal)).ToArray();

        missing.ShouldBeEmpty($"the README omits: {string.Join(", ", missing)}");
    }

    [Fact]
    public void TheReadmeExampleIsWhatTheToolPrintsForTheFixture()
    {
        // The example is real output, and stays so: it is re-run here against the fixture it
        // names. Only the export line differs, because the README runs it from the fixtures folder.
        var lines = Readme.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line == "$ gobd-validate broken-export --contents");
        start.ShouldBeGreaterThan(0, "the README has no example run");
        var end = Array.FindIndex(lines, start, line => line.StartsWith("```", StringComparison.Ordinal));
        var documented = lines[(start + 1)..end];

        var fixture = Fixtures.FixtureLibrary.FolderPath(Fixtures.FixtureLibrary.BrokenExport);
        var actual = Cli.CliHarness.Run(fixture, "--contents", "--language", "en").Output
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("Export: " + fixture, "Export: broken-export", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');

        actual.ShouldBe(documented);
    }
}
