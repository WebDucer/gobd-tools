using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Findings;
using GoBd.Validation.Tests.Cli;
using GoBd.Validation.Tests.Fixtures;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// An export whose amounts carry their sign behind the digits, as the standard permits, read end
/// to end by both tools.
/// </summary>
/// <remarks>
/// Built from the conformant fixture with nothing changed but the signs, so whatever either tool
/// says about it can only be about the signs. Before trailing signs were read, the validator
/// reported both amounts as <c>GOBD4001</c> and the reader withheld the whole table.
/// </remarks>
public sealed class TrailingSignEndToEndTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), $"gobd-signs-{Guid.NewGuid():N}");

    public TrailingSignEndToEndTests()
    {
        Copy(FixtureLibrary.FolderPath("good-export"), Export);

        var data = Path.Combine(Export, "data", "bestellungen.csv");
        var signed = File.ReadAllText(data)
            .Replace("12,50", "12,50-", StringComparison.Ordinal)
            .Replace("9,99", "9,99+", StringComparison.Ordinal);
        File.WriteAllText(data, signed);
    }

    private string Export => Path.Combine(workspace, "export");

    public void Dispose() => Directory.Delete(workspace, recursive: true);

    [Fact]
    public void TheValidatorFindsTheExportConformant()
    {
        var result = CliHarness.Run(Export, "--contents");

        result.Output.ShouldNotContain(FindingCodes.RecordValueTypeMismatch);
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public void TheReaderShowsTheTableAsData()
    {
        var options = new StoreOptions(Path.Combine(workspace, "stores"));
        using var session = ReaderSession.Open(Export, options).Session.ShouldNotBeNull();
        session.Read(cancellationToken: TestContext.Current.CancellationToken);

        var bestellungen = session.DataSet.Tables.Single(table => table.Identity == "Bestellungen");
        session.View(bestellungen).Kind.ShouldBe(TableViewKind.Data);
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
        }
    }
}
