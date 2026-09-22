using GoBd.Validation.Cli.Reporting;
using GoBd.Validation.Findings;
using GoBd.Validation.Localisation;

namespace GoBd.Validation.Cli;

/// <summary>
/// The command itself, with its streams injected so that it can be driven from tests exactly as
/// it is from a terminal.
/// </summary>
public static class CliRunner
{
    /// <summary>Runs the validator and returns the process exit code.</summary>
    public static int Run(IReadOnlyList<string> arguments, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var parsed = CommandLine.Parse(arguments);
        if (parsed.Options is not { } options)
        {
            error.WriteLine(parsed.Error);
            WriteUsage(error);
            return (int)ExitCode.ToolFailure;
        }

        if (options.ShowVersion)
        {
            output.WriteLine(Version);
            return (int)ExitCode.Conformant;
        }

        // The executable is delivered alone, so it carries its licence, what that licence does not
        // cover, and the notices its components require, and prints each of them itself. They stay
        // apart as the files do. Asking for one validates nothing. See the prepare-public-release
        // change's design.md D4.
        if (options.ShowLicense)
        {
            output.Write(LicenceTexts.Licence);
            return (int)ExitCode.Conformant;
        }

        if (options.ShowNotice)
        {
            output.Write(LicenceTexts.Notice);
            return (int)ExitCode.Conformant;
        }

        if (options.ShowNotices)
        {
            output.Write(LicenceTexts.ThirdPartyNotices);
            return (int)ExitCode.Conformant;
        }

        if (options.ShowHelp || options.ExportPath is null)
        {
            WriteUsage(output);
            return options.ShowHelp ? (int)ExitCode.Conformant : (int)ExitCode.ToolFailure;
        }

        var language = LanguageResolver.Resolve(options.Language);
        var report = ExportValidator.Validate(
            options.ExportPath,
            options.Strict,
            language.Finding is null ? [] : [language.Finding],
            options.Contents);

        IReportWriter writer = options.Format == ReportFormat.Json
            ? new JsonReportWriter()
            : new TextReportWriter();

        if (options.OutputPath is { } destination)
        {
            using (var file = new StreamWriter(destination, append: false))
            {
                writer.Write(file, report, language.Language);
            }

            // The report went to the file, so standard output carries only a short summary.
            output.WriteLine(Summary(report, destination, language.Language));
        }
        else
        {
            writer.Write(output, report, language.Language);
        }

        return (int)ExitCodes.For(report);
    }

    private static string Version => typeof(CliRunner).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// The one line a caller who redirected the report to a file actually sees.
    /// </summary>
    /// <remarks>
    /// Routed through <see cref="ReportText"/> like every other user-facing string. It was
    /// previously hardcoded English, so a German run printed an English summary pointing at a
    /// German report.
    /// </remarks>
    private static string Summary(ValidationReport report, string destination, ReportLanguage language)
    {
        var verdict = report.Verdict == Verdict.Conformant
            ? ReportText.Conformant(language)
            : ReportText.NonConformant(language);
        var counts = ReportText.Counts(language, report.ErrorCount, report.WarningCount, report.InfoCount);
        return $"{verdict}: {counts}. {ReportText.WrittenTo(language, destination)}";
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("gobd-validate - validate a GoBD/GDPdU data carrier export");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  gobd-validate <export> [options]");
        writer.WriteLine();
        writer.WriteLine("  <export>  A ZIP archive or an unpacked folder containing index.xml.");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  --format <text|json>   Report format. Default: text.");
        writer.WriteLine("  -o, --output <file>    Write the report to a file instead of standard output.");
        writer.WriteLine("  -l, --language <en|de> Report language. Default: the operating system's, else English.");
        writer.WriteLine("                         May also be set with the GOBD_LANG environment variable.");
        writer.WriteLine("      --strict           Count warnings against the verdict.");
        writer.WriteLine("      --contents         Also read the data files and check what they contain:");
        writer.WriteLine("                         record layout, header rows, primary key uniqueness and");
        writer.WriteLine("                         foreign key existence. Off by default.");
        writer.WriteLine("      --max-findings <n> Findings per table after which analysis of that table");
        writer.WriteLine("                         stops. Default: 50. Only meaningful with --contents.");
        writer.WriteLine("  -h, --help             Show this help.");
        writer.WriteLine("      --version          Show the version.");
        writer.WriteLine("      --license          Show the licence: MIT.");
        writer.WriteLine("      --notice           Show what that licence does not cover.");
        writer.WriteLine("      --third-party-notices");
        writer.WriteLine("                         Show the notices of the components included.");
        writer.WriteLine();
        writer.WriteLine("Exit codes:");
        writer.WriteLine("  0  conformant       1  warnings only");
        writer.WriteLine("  2  errors present   3  the validator could not run");
        writer.WriteLine();
        // The boundary of the check has to be discoverable from the command line itself:
        // a clean result without --contents does not mean the data files are well formed.
        writer.WriteLine("Scope:");
        writer.WriteLine("  Without --contents, validation covers index.xml, the DTD and the export's");
        writer.WriteLine("  file listing. The contents of the data files are NOT examined.");
        writer.WriteLine("  With --contents, every declared data file is read as well, and its records,");
        writer.WriteLine("  header row, primary keys and foreign key values are checked against the");
        writer.WriteLine("  declaration. Analysis of a table stops after --max-findings findings, so a");
        writer.WriteLine("  wholly defective file is reported without being read to its end.");
        writer.WriteLine();
        writer.WriteLine("Limits:");
        writer.WriteLine("  The key checks hold every key of one table in memory, eight bytes each. A");
        writer.WriteLine("  table beyond 64 million records is reported as unchecked (GOBD9006, exit 3)");
        writer.WriteLine("  rather than reported as clean. The desktop reader has no such ceiling.");
    }
}
