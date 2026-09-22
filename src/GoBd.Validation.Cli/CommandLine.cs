using System.Globalization;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Cli.Reporting;

namespace GoBd.Validation.Cli;

/// <summary>Options parsed from the command line.</summary>
/// <param name="ExportPath">Path of the export to validate.</param>
/// <param name="Format">Report format.</param>
/// <param name="OutputPath">File to write the report to, or null to write to standard output.</param>
/// <param name="Strict">Whether warnings count against the verdict.</param>
/// <param name="Language">Requested report language, or null to resolve it from the environment.</param>
/// <param name="ShowHelp">Whether the caller asked for usage.</param>
/// <param name="ShowVersion">Whether the caller asked for the version.</param>
/// <param name="CheckContents">Whether to read the data files as well as what describes them.</param>
/// <param name="MaximumFindingsPerTable">Findings per table after which analysis stops.</param>
/// <param name="ShowLicense">Whether the caller asked for the licence.</param>
/// <param name="ShowNotice">Whether the caller asked for what the licence does not cover.</param>
/// <param name="ShowNotices">Whether the caller asked for the notices of the third-party components.</param>
public sealed record CommandLineOptions(
    string? ExportPath = null,
    ReportFormat Format = ReportFormat.Text,
    string? OutputPath = null,
    bool Strict = false,
    string? Language = null,
    bool ShowHelp = false,
    bool ShowVersion = false,
    bool CheckContents = false,
    int MaximumFindingsPerTable = ContentOptions.DefaultMaximumFindingsPerTable,
    bool ShowLicense = false,
    bool ShowNotice = false,
    bool ShowNotices = false)
{
    /// <summary>How to check the data files, or null when they are to be left unopened.</summary>
    public ContentOptions? Contents =>
        CheckContents ? new ContentOptions(MaximumFindingsPerTable) : null;
}

/// <summary>Outcome of parsing a command line.</summary>
/// <param name="Options">The parsed options, when parsing succeeded.</param>
/// <param name="Error">Why parsing failed, when it did.</param>
public sealed record CommandLineResult(CommandLineOptions? Options, string? Error);

/// <summary>
/// Parses the command line by hand.
/// </summary>
/// <remarks>
/// The surface is one argument and five options, so a parsing library would add a dependency
/// and NativeAOT surface for no benefit.
/// </remarks>
public static class CommandLine
{
    /// <summary>Parses the arguments.</summary>
    public static CommandLineResult Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var options = new CommandLineOptions();

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "-h" or "--help":
                    options = options with { ShowHelp = true };
                    break;

                case "--version":
                    options = options with { ShowVersion = true };
                    break;

                case "--license":
                    options = options with { ShowLicense = true };
                    break;

                case "--notice":
                    options = options with { ShowNotice = true };
                    break;

                case "--third-party-notices":
                    options = options with { ShowNotices = true };
                    break;

                case "--strict":
                    options = options with { Strict = true };
                    break;

                case "--contents":
                    options = options with { CheckContents = true };
                    break;

                case "--max-findings":
                    if (!TryTake(arguments, ref index, out var bound))
                    {
                        return Failure("--max-findings requires a number.");
                    }

                    if (!int.TryParse(bound, NumberStyles.None, CultureInfo.InvariantCulture, out var maximum)
                        || maximum < 1)
                    {
                        return Failure($"'{bound}' is not a usable maximum. Give a whole number of at least 1.");
                    }

                    options = options with { MaximumFindingsPerTable = maximum };
                    break;

                case "--format":
                    if (!TryTake(arguments, ref index, out var format))
                    {
                        return Failure("--format requires a value (text or json).");
                    }

                    switch (format.ToLowerInvariant())
                    {
                        case "text": options = options with { Format = ReportFormat.Text }; break;
                        case "json": options = options with { Format = ReportFormat.Json }; break;
                        default: return Failure($"Unknown report format '{format}'. Use text or json.");
                    }

                    break;

                case "--output" or "-o":
                    if (!TryTake(arguments, ref index, out var output))
                    {
                        return Failure("--output requires a file path.");
                    }

                    options = options with { OutputPath = output };
                    break;

                case "--language" or "-l":
                    if (!TryTake(arguments, ref index, out var language))
                    {
                        return Failure("--language requires a value (en or de).");
                    }

                    options = options with { Language = language };
                    break;

                default:
                    if (argument.StartsWith('-'))
                    {
                        return Failure($"Unknown option '{argument}'.");
                    }

                    if (options.ExportPath is not null)
                    {
                        return Failure("Only one export path may be given.");
                    }

                    options = options with { ExportPath = argument };
                    break;
            }
        }

        return new CommandLineResult(options, null);
    }

    private static bool TryTake(IReadOnlyList<string> arguments, ref int index, out string value)
    {
        if (index + 1 >= arguments.Count)
        {
            value = string.Empty;
            return false;
        }

        value = arguments[++index];
        return true;
    }

    private static CommandLineResult Failure(string message) => new(null, message);
}
