using System.Reflection;
using System.Text.RegularExpressions;
using GoBd.Validation.Findings;

namespace GoBd.Validation.Tests.Documentation;

/// <summary>One documented code, as the reference states it.</summary>
/// <param name="Code">The code its heading names.</param>
/// <param name="Severity">Severity stated on the entry's label line.</param>
/// <param name="Category">Category stated on the entry's label line.</param>
public sealed record DocumentedCode(string Code, string? Severity, string? Category);

/// <summary>
/// Holds <c>docs/finding-codes.md</c> to the code catalogue.
/// </summary>
/// <remarks>
/// A coverage test that parses nothing compares an empty set against an empty set and passes,
/// proving only that it ran. The assertions below therefore establish that parsing worked before
/// they compare anything. See the change's design.md D2.
/// </remarks>
public sealed partial class FindingCodeReferenceTests
{
    [GeneratedRegex(@"^### (GOBD\d{4}) — .+$", RegexOptions.Multiline)]
    private static partial Regex EntryHeading();

    [GeneratedRegex(@"\*\*Severity:\*\*\s*(\w+)\s+\*\*Category:\*\*\s*(\w+)", RegexOptions.Multiline)]
    private static partial Regex Labels();

    private static string ReferencePath { get; } = Locate();

    private static IReadOnlyList<DocumentedCode> Documented { get; } = Parse(File.ReadAllText(ReferencePath));

    private static string Locate()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        for (var candidate = new DirectoryInfo(directory); candidate is not null; candidate = candidate.Parent)
        {
            var path = Path.Combine(candidate.FullName, "docs", "finding-codes.md");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("docs/finding-codes.md was not found.");
    }

    /// <summary>
    /// Splits the document into entries at each code heading.
    /// </summary>
    /// <remarks>
    /// Entries are found by heading, not by scanning for codes anywhere in the text, so a code
    /// mentioned inside another entry's prose is not mistaken for an entry of its own.
    /// </remarks>
    internal static IReadOnlyList<DocumentedCode> Parse(string markdown)
    {
        var headings = EntryHeading().Matches(markdown);
        var entries = new List<DocumentedCode>(headings.Count);

        for (var index = 0; index < headings.Count; index++)
        {
            var start = headings[index].Index;
            var end = index + 1 < headings.Count ? headings[index + 1].Index : markdown.Length;
            var body = markdown[start..end];
            var labels = Labels().Match(body);

            entries.Add(new DocumentedCode(
                headings[index].Groups[1].Value,
                labels.Success ? labels.Groups[1].Value : null,
                labels.Success ? labels.Groups[2].Value : null));
        }

        return entries;
    }

    // ---- the guard on the guard ------------------------------------------------------------

    [Fact]
    public void TheReferenceParsesToANonEmptySetOfEntries() => Documented.ShouldNotBeEmpty();

    [Fact]
    public void ParsingADocumentWithNoEntriesYieldsNothing() =>
        // Proves the parser can fail. Without this, a heading-format change would silently turn
        // every assertion below into a comparison of two empty sets.
        Parse("# Finding codes\n\nNo entries here at all.\n").ShouldBeEmpty();

    [Fact]
    public void EveryEntryStatesItsSeverityAndCategory() =>
        Documented.ShouldAllBe(entry => entry.Severity != null && entry.Category != null);

    [Fact]
    public void TheReferenceDocumentsAsManyCodesAsTheCatalogueHolds() =>
        Documented.Count.ShouldBe(FindingCodes.All.Length);

    // ---- two-way coverage --------------------------------------------------------------------

    [Fact]
    public void EveryCatalogueCodeIsDocumented()
    {
        var documented = Documented.Select(entry => entry.Code).ToHashSet(StringComparer.Ordinal);
        var undocumented = FindingCodes.All
            .Select(info => info.Code)
            .Where(code => !documented.Contains(code))
            .ToArray();

        undocumented.ShouldBeEmpty($"undocumented codes: {string.Join(", ", undocumented)}");
    }

    [Fact]
    public void TheReferenceDocumentsNoCodeThatDoesNotExist()
    {
        var stale = Documented
            .Select(entry => entry.Code)
            .Where(code => !FindingCodes.IsKnown(code))
            .ToArray();

        stale.ShouldBeEmpty($"documented but not in the catalogue: {string.Join(", ", stale)}");
    }

    [Fact]
    public void NoCodeIsDocumentedTwice()
    {
        var duplicates = Documented
            .GroupBy(entry => entry.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        duplicates.ShouldBeEmpty();
    }

    // ---- the reference must agree with the catalogue ----------------------------------------

    [Fact]
    public void EveryEntryStatesTheCataloguesSeverityAndCategory()
    {
        var mismatches = new List<string>();
        foreach (var entry in Documented.Where(entry => FindingCodes.IsKnown(entry.Code)))
        {
            var info = FindingCodes.Describe(entry.Code);
            if (!string.Equals(entry.Severity, info.DefaultSeverity.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add($"{entry.Code}: says {entry.Severity}, catalogue says {info.DefaultSeverity}");
            }

            if (!string.Equals(entry.Category, info.Category.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add($"{entry.Code}: says {entry.Category}, catalogue says {info.Category}");
            }
        }

        mismatches.ShouldBeEmpty(string.Join("; ", mismatches));
    }

    [Fact]
    public void EntriesAppearInAscendingCodeOrder() =>
        Documented.Select(entry => entry.Code)
            .ShouldBe(Documented.Select(entry => entry.Code).OrderBy(code => code, StringComparer.Ordinal));
}
