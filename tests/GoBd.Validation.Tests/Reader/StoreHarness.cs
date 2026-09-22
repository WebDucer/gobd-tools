using System.Text;
using GoBd.Reader.Data;
using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Dtd;
using GoBd.Validation.Model;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// A throwaway export on disk, its parsed description, and a store rooted somewhere equally
/// throwaway — so that no test can leave anything behind or read another test's store.
/// </summary>
public sealed class StoreHarness : IDisposable
{
    private readonly List<IDisposable> open = [];

    private StoreHarness(string exportPath, string storeRoot, DataSetNode dataSet)
    {
        ExportPath = exportPath;
        StoreRoot = storeRoot;
        DataSet = dataSet;
        Options = new StoreOptions(storeRoot);
    }

    /// <summary>Where the export was written.</summary>
    public string ExportPath { get; }

    /// <summary>Where stores are rooted for this test.</summary>
    public string StoreRoot { get; }

    /// <summary>The parsed description.</summary>
    public DataSetNode DataSet { get; }

    /// <summary>Store options pointing at this test's own root.</summary>
    public StoreOptions Options { get; }

    /// <summary>Builds an export from table declarations and their files.</summary>
    public static StoreHarness Create(string tablesXml, params (string Name, string Content)[] files) =>
        Create(tablesXml, Encoding.Latin1, files);

    /// <summary>Builds an export whose data files are written in the given encoding.</summary>
    public static StoreHarness Create(
        string tablesXml,
        Encoding encoding,
        params (string Name, string Content)[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(encoding);
        return CreateRaw(tablesXml, [.. files.Select(file => (file.Name, encoding.GetBytes(file.Content)))]);
    }

    /// <summary>Builds an export whose data files are given as bytes.</summary>
    public static StoreHarness CreateRaw(string tablesXml, params (string Name, byte[] Bytes)[] files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var root = Path.Combine(Path.GetTempPath(), "gobd-store-" + Guid.NewGuid().ToString("N"));
        var exportPath = Path.Combine(root, "export");
        var storeRoot = Path.Combine(root, "stores");
        Directory.CreateDirectory(exportPath);
        CreatePrivate(storeRoot);

        var document = IndexXml.Document($"""
              <Version>1.0</Version>
              <Media>
                <Name>Disk 1</Name>
            {tablesXml}
              </Media>
            """);

        File.WriteAllText(Path.Combine(exportPath, "index.xml"), document, Encoding.UTF8);
        File.WriteAllBytes(Path.Combine(exportPath, CanonicalDtd.FileName), CanonicalDtd.Bytes.ToArray());
        foreach (var (name, bytes) in files)
        {
            var path = Path.Combine(exportPath, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        var dataSet = IndexXmlParser.Parse(IndexXml.Stream(document)).DataSet
            ?? throw new InvalidOperationException("The declaration is not well formed.");

        return new StoreHarness(exportPath, storeRoot, dataSet);
    }

    /// <summary>A source over the export, disposed with the harness.</summary>
    public IExportSource Source()
    {
        var source = new FolderExportSource(ExportPath);
        open.Add(source);
        return source;
    }

    /// <summary>Opens a store over the export, disposed with the harness.</summary>
    public ExportStore Store(StoreOptions? options = null)
    {
        var result = ExportStore.Open(Source(), DataSet, options ?? Options);
        var store = result.Store ?? throw new InvalidOperationException("The store refused to open.");
        open.Add(store);
        return store;
    }

    /// <summary>Runs library checks over the same export, for holding the two engines together.</summary>
    public IReadOnlyList<Finding> Streaming(ContentOptions options, params ICheck[] checks) =>
        CheckEngine.Run(checks, new CheckContext(DataSet, Source(), options));

    /// <summary>Imports every declared table and returns what the store made of them.</summary>
    public IReadOnlyList<Finding> StoreFindings(ExportStore store, int bound)
    {
        ArgumentNullException.ThrowIfNull(store);

        var findings = new List<Finding>();
        foreach (var table in DataSet.Tables)
        {
            findings.AddRange(store.Import(table, bound).Findings);
        }

        findings.AddRange(StoreKeyChecks.Run(store, DataSet, bound));
        return findings;
    }

    /// <summary>The declared table with the given identity.</summary>
    public TableNode Table(string identity) =>
        DataSet.Tables.First(table => string.Equals(table.Identity, identity, StringComparison.Ordinal));

    /// <summary>Files and directories anywhere under the export, for asserting nothing was added.</summary>
    public IReadOnlyList<string> ExportContents() =>
        [.. Directory.EnumerateFileSystemEntries(ExportPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(ExportPath, path))
            .Order(StringComparer.Ordinal)];

    public void Dispose()
    {
        foreach (var item in Enumerable.Reverse(open))
        {
            item.Dispose();
        }

        var root = Directory.GetParent(ExportPath)!.FullName;
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
