using System.IO.Compression;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// Opening an export from within the reader: either form the standard permits, a dismissed
/// choice, a wrong choice, and one export replacing another.
/// </summary>
public sealed class ReaderWorkspaceTests
{
    private const string Kunden = """
            <Table>
              <URL>kunden.csv</URL>
              <Name>Kunden</Name>
              <VariableLength>
                <VariablePrimaryKey><Name>Code</Name><AlphaNumeric/></VariablePrimaryKey>
                <VariableColumn><Name>Firma</Name><AlphaNumeric/></VariableColumn>
              </VariableLength>
            </Table>
    """;

    private static StoreHarness Export() => StoreHarness.Create(Kunden, ("kunden.csv", "K1;Meier\r\nK2;Lang\r\n"));

    /// <summary>A path beside the export, inside the harness's throwaway root.</summary>
    private static string Beside(StoreHarness harness, string name) =>
        Path.GetFullPath(Path.Combine(harness.ExportPath, "..", name));

    private static string Zipped(StoreHarness harness)
    {
        var zip = Beside(harness, "export.zip");
        ZipFile.CreateFromDirectory(harness.ExportPath, zip);
        return zip;
    }

    private static int Stores(StoreHarness harness) => Directory.EnumerateDirectories(harness.StoreRoot).Count();

    /// <summary>
    /// Opens an export and waits for it to be read.
    /// </summary>
    /// <remarks>
    /// The workspace starts reading on a worker and returns at once, because the window must stay
    /// responsive. A test asserting on what a table shows waits for that worker instead.
    /// </remarks>
    private static WorkspaceOpenResult Read(ReaderWorkspace workspace, string? path)
    {
        var result = workspace.Open(path);
        workspace.Reading.Wait(TestContext.Current.CancellationToken);
        return result;
    }

    // ---- 12.1 either form ----------------------------------------------------------------------

    [Fact]
    public void AnUnpackedExportOpensFromAFolder()
    {
        using var harness = Export();
        using var workspace = new ReaderWorkspace(harness.Options);

        workspace.Open(harness.ExportPath).Outcome.ShouldBe(OpenOutcome.Opened);

        workspace.Session.ShouldNotBeNull().Navigator.ShouldHaveSingleItem().Tables
            .ShouldHaveSingleItem().Identity.ShouldBe("Kunden");
    }

    [Fact]
    public void APackagedExportOpensFromAnArchiveAndItsTablesRead()
    {
        // The store extracts every table, so a ZIP member reaches it through the same path as a
        // file on disk. This is the proof that it does.
        using var harness = Export();
        using var workspace = new ReaderWorkspace(harness.Options);

        Read(workspace, Zipped(harness)).Outcome.ShouldBe(OpenOutcome.Opened);

        var session = workspace.Session.ShouldNotBeNull();
        var view = session.View(session.DataSet.Tables.Single());
        view.Kind.ShouldBe(TableViewKind.Data);
        view.Rows.ShouldNotBeNull().Count.ShouldBe(2);
    }

    // ---- 12.2 choosing nothing -----------------------------------------------------------------

    [Fact]
    public void DismissingTheChoiceLeavesWhatIsOpenUntouched()
    {
        using var harness = Export();
        using var workspace = new ReaderWorkspace(harness.Options);
        Read(workspace, harness.ExportPath);
        var session = workspace.Session.ShouldNotBeNull();
        var directory = session.Store.Directory;
        var view = session.View(session.DataSet.Tables.Single());

        workspace.Open(null).Outcome.ShouldBe(OpenOutcome.Dismissed);

        workspace.Session.ShouldBeSameAs(session);
        Directory.Exists(directory).ShouldBeTrue();
        session.View(session.DataSet.Tables.Single()).ShouldBeSameAs(view);
    }

    // ---- 12.3 choosing something that is not an export -----------------------------------------

    [Fact]
    public void AFolderWithNoIndexSaysSoAndOpensNothing()
    {
        using var harness = Export();
        using var workspace = new ReaderWorkspace(harness.Options);
        var folder = Beside(harness, "not-an-export");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "nothing to see");

        var result = workspace.Open(folder);

        result.Outcome.ShouldBe(OpenOutcome.Refused);
        result.Findings.ShouldContain(finding => finding.Code == FindingCodes.IndexMissing);
        workspace.Session.ShouldBeNull();
        Stores(harness).ShouldBe(0);
    }

    [Fact]
    public void AFileThatIsNotAnArchiveIsReportedAsUnreadable()
    {
        using var harness = Export();
        using var workspace = new ReaderWorkspace(harness.Options);
        var file = Beside(harness, "not-a-zip.zip");
        File.WriteAllText(file, "hello");

        var result = workspace.Open(file);

        result.Outcome.ShouldBe(OpenOutcome.Refused);
        result.Findings.ShouldContain(finding => finding.Code == FindingCodes.ExportUnreadable);
    }

    [Fact]
    public void AWrongChoiceKeepsTheOpenExportAndSaysWhy()
    {
        // A mistaken click must not cost the person the medium they were reading.
        using var harness = Export();
        using var workspace = new ReaderWorkspace(harness.Options);
        workspace.Open(harness.ExportPath);
        var session = workspace.Session.ShouldNotBeNull();

        var result = workspace.Open(Beside(harness, "does-not-exist"));

        result.Outcome.ShouldBe(OpenOutcome.Refused);
        result.Findings.ShouldContain(finding => finding.Code == FindingCodes.ExportPathNotFound);
        workspace.Session.ShouldBeSameAs(session);
        Directory.Exists(session.Store.Directory).ShouldBeTrue();
    }

    // ---- 12.4 one export at a time -------------------------------------------------------------

    [Fact]
    public void OpeningASecondExportReleasesTheFirstAndItsStore()
    {
        using var first = Export();
        using var second = Export();
        using var workspace = new ReaderWorkspace(first.Options);

        workspace.Open(first.ExportPath);
        var firstStore = workspace.Session.ShouldNotBeNull().Store.Directory;

        workspace.Open(second.ExportPath).Outcome.ShouldBe(OpenOutcome.Opened);

        Directory.Exists(firstStore).ShouldBeFalse();
        Stores(first).ShouldBe(1);

        workspace.Dispose();
        Stores(first).ShouldBe(0);
    }

    // ---- 12.5 argument and choice ----------------------------------------------------------------

    [Fact]
    public void OpeningByArgumentAndByChoiceReachTheSameState()
    {
        // A picker may hand a folder back with a trailing separator where a person typing the
        // argument would not. The same folder has to open the same way either way.
        using var harness = Export();
        using var typed = new ReaderWorkspace(harness.Options);
        using var chosen = new ReaderWorkspace(harness.Options);

        Read(typed, harness.ExportPath).Outcome.ShouldBe(OpenOutcome.Opened);
        Read(chosen, harness.ExportPath + Path.DirectorySeparatorChar).Outcome.ShouldBe(OpenOutcome.Opened);

        chosen.ExportPath.ShouldBe(typed.ExportPath);
        var fromChoice = chosen.Session.ShouldNotBeNull();
        var fromArgument = typed.Session.ShouldNotBeNull();
        fromChoice.Navigator.Select(medium => medium.Name).ShouldBe(fromArgument.Navigator.Select(medium => medium.Name));
        fromChoice.View(fromChoice.DataSet.Tables.Single()).Rows.ShouldNotBeNull().Count
            .ShouldBe(fromArgument.View(fromArgument.DataSet.Tables.Single()).Rows.ShouldNotBeNull().Count);
    }
}
