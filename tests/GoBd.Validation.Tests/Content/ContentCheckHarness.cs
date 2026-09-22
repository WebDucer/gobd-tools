using System.Text;
using GoBd.Validation.Checks;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;
using GoBd.Validation.Parsing;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Parsing;

namespace GoBd.Validation.Tests.Content;

/// <summary>
/// Runs a content check over a throwaway export: a declaration, and the data files it declares.
/// </summary>
/// <remarks>
/// The files are written to disk rather than faked in memory because the check reaches them
/// through <see cref="IExportSource"/>, and a test that bypassed the source would not exercise
/// the path a real run takes.
/// </remarks>
public static class ContentCheckHarness
{
    /// <summary>Runs a check over the given declaration and files.</summary>
    public static IReadOnlyList<Finding> Run(
        ICheck check,
        string tablesXml,
        (string Name, byte[] Bytes)[] files,
        ContentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        var root = Path.Combine(Path.GetTempPath(), "gobd-content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var (name, bytes) in files)
            {
                var path = Path.Combine(root, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }

            var document = IndexXml.Document($"""
                  <Version>1.0</Version>
                  <Media>
                    <Name>Disk 1</Name>
                {tablesXml}
                  </Media>
                """);

            var outcome = IndexXmlParser.Parse(IndexXml.Stream(document));
            var dataSet = outcome.DataSet
                ?? throw new InvalidOperationException("The declaration is not well formed.");

            using var source = new FolderExportSource(root);
            return CheckEngine.Run([check], new CheckContext(dataSet, source, options));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Runs a check over one declared table whose file holds the given text.</summary>
    public static IReadOnlyList<Finding> Run(
        ICheck check,
        string tableXml,
        string fileName,
        string content,
        ContentOptions? options = null) =>
        Run(check, tableXml, [(fileName, Encoding.Latin1.GetBytes(content))], options);

    /// <summary>The codes a run produced, in order.</summary>
    public static IReadOnlyList<string> Codes(IEnumerable<Finding> findings) =>
        [.. findings.Select(finding => finding.Code)];
}
