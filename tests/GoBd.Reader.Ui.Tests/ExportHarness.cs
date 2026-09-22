using System.Text;
using Avalonia.Controls;
using GoBd.Reader.Data;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Dtd;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.Tests;

/// <summary>
/// A throwaway export on disk, opened and read, for driving the controls against.
/// </summary>
/// <remarks>
/// The controls are worth testing against a real export rather than a stand-in: what a filter
/// editor offers comes from the declaration, what it accepts comes from the table's declared
/// symbols, and what applying one does comes from the store. A fake of any of those would test
/// the fake.
/// <para>
/// Its own harness rather than the unit tests', which is the price of keeping the two projects
/// apart: those tests need no window and must stay quick, and this one stands up a windowing
/// platform.
/// </para>
/// </remarks>
public sealed class ExportHarness : IDisposable
{
    private readonly string root;
    private readonly List<Window> windows = [];
    private ReaderSession? session;

    private ExportHarness(string root, string exportPath, StoreOptions options)
    {
        this.root = root;
        ExportPath = exportPath;
        Options = options;
    }

    /// <summary>Where the export was written.</summary>
    public string ExportPath { get; }

    /// <summary>Store options pointing at this test's own root.</summary>
    public StoreOptions Options { get; }

    /// <summary>Writes an export from table declarations and their files.</summary>
    public static ExportHarness Create(string tablesXml, params (string Name, string Content)[] files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var root = Path.Combine(Path.GetTempPath(), "gobd-ui-" + Guid.NewGuid().ToString("N"));
        var exportPath = Path.Combine(root, "export");
        var stores = Path.Combine(root, "stores");
        Directory.CreateDirectory(exportPath);
        CreatePrivate(stores);

        var document = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE DataSet SYSTEM "{CanonicalDtd.FileName}">
            <DataSet>
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
            {tablesXml}
              </Media>
            </DataSet>
            """;

        File.WriteAllText(Path.Combine(exportPath, "index.xml"), document, Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(exportPath, CanonicalDtd.FileName), CanonicalDtd.Bytes.ToArray());

        foreach (var (name, content) in files)
        {
            // The standard's default codepage where none is declared, which is what these
            // declarations leave unsaid.
            File.WriteAllBytes(Path.Combine(exportPath, name), Encoding.Latin1.GetBytes(content));
        }

        return new ExportHarness(root, exportPath, new StoreOptions(stores));
    }

    /// <summary>Opens the export and reads it to the end, as the window's worker would.</summary>
    public ReaderSession Read()
    {
        var opened = ReaderSession.Open(ExportPath, Options).Session
            ?? throw new InvalidOperationException("The export could not be opened.");

        opened.Read();
        session = opened;
        return opened;
    }

    /// <summary>
    /// Takes charge of a window shown against this export, so it goes before the store does.
    /// </summary>
    /// <remarks>
    /// The headless session lives for the whole assembly, so a window left open is laid out again
    /// in later tests. A grid still bound to a closed export's store then pages a store that is
    /// gone — a fault of the test that left it open, not of the reader, which closes its windows
    /// before it closes anything they are reading.
    /// </remarks>
    public void Track(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        windows.Add(window);
    }

    /// <summary>The declared table with the given identity.</summary>
    public static TableNode Table(ReaderSession opened, string identity)
    {
        ArgumentNullException.ThrowIfNull(opened);
        return opened.DataSet.Tables.First(table =>
            string.Equals(table.Identity, identity, StringComparison.Ordinal));
    }

    public void Dispose()
    {
        // Windows first: nothing may be laid out against a store that is about to go.
        foreach (var window in windows)
        {
            window.Content = null;
            window.Close();
        }

        windows.Clear();
        session?.Dispose();

        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// Creates the store root readable by this account alone, as the store would: it refuses a
    /// root that other accounts can read. See the prepare-public-release change's design.md D6.
    /// </summary>
    internal static void CreatePrivate(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else
        {
            Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
