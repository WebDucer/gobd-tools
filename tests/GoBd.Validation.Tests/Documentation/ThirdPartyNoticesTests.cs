using System.Reflection;
using System.Text.Json;

namespace GoBd.Validation.Tests.Documentation;

/// <summary>
/// Holds <c>THIRD-PARTY-NOTICES.txt</c> to the packages the tools actually ship.
/// </summary>
/// <remarks>
/// A package whose licence asks for its notice to travel with every copy is easy to acquire and
/// easy to forget: it arrives with an upgrade or as a dependency of a dependency, and nothing
/// announces it. So the packages each tool's build resolves are compared with the ones the notices
/// name, in both directions: a shipped package that is not named fails, and so does a named one
/// that no build ships any more. A package ships when its resolved entry carries runtime, native or
/// runtime-specific assets; one that only takes part in building carries none. See the
/// prepare-public-release change's design.md D3.
/// </remarks>
public sealed class ThirdPartyNoticesTests
{
    private const string BothTools = "In gobd-validate and gobd-reader";

    private static string Root { get; } = LocateRoot();

    private static string LocateRoot()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        for (var candidate = new DirectoryInfo(directory); candidate is not null; candidate = candidate.Parent)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "GoBdReader.slnx")))
            {
                return candidate.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root was not found.");
    }

    /// <summary>The packages a project's restored build ships, from its assets file.</summary>
    private static SortedSet<string> Shipped(string project)
    {
        var assets = Path.Combine(Root, "src", project, "obj", "project.assets.json");
        File.Exists(assets).ShouldBeTrue($"{assets} is missing; restore the solution first");

        using var document = JsonDocument.Parse(File.ReadAllText(assets));
        var shipped = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var target in document.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (var library in target.Value.EnumerateObject())
            {
                if (library.Value.GetProperty("type").GetString() == "package" && CarriesAssets(library.Value))
                {
                    shipped.Add(library.Name.Split('/')[0]);
                }
            }
        }

        return shipped;
    }

    private static bool CarriesAssets(JsonElement library)
    {
        foreach (var kind in new[] { "runtime", "native", "runtimeTargets" })
        {
            if (library.TryGetProperty(kind, out var assets)
                && assets.EnumerateObject().Any(asset => !asset.Name.EndsWith("/_._", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The package ids each section of the notices names on its <c>Packages:</c> lines.</summary>
    private static Dictionary<string, SortedSet<string>> Named()
    {
        var lines = File.ReadAllLines(Path.Combine(Root, "THIRD-PARTY-NOTICES.txt"));
        var named = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var section = string.Empty;
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith("====", StringComparison.Ordinal)
                && index + 2 < lines.Length
                && lines[index + 2].StartsWith("====", StringComparison.Ordinal))
            {
                section = lines[index + 1];
                index += 2;
                continue;
            }

            const string prefix = "Packages: ";
            if (!lines[index].StartsWith(prefix, StringComparison.Ordinal)
                || lines[index].StartsWith(prefix + "(", StringComparison.Ordinal))
            {
                continue;
            }

            if (!named.TryGetValue(section, out var ids))
            {
                named[section] = ids = new SortedSet<string>(StringComparer.Ordinal);
            }

            foreach (var id in lines[index][prefix.Length..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                ids.Add(id);
            }
        }

        return named;
    }

    private static SortedSet<string> AllNamed() => new(Named().Values.SelectMany(ids => ids), StringComparer.Ordinal);

    [Fact]
    public void TheReaderShipsPackages() =>
        // Guard on the guard: an assets file read wrongly would make every comparison vacuous.
        Shipped("GoBd.Reader.Ui").ShouldContain("DuckDB.NET.Bindings.Full");

    [Fact]
    public void BuildOnlyPackagesAreNotCountedAsShipped()
    {
        Shipped("GoBd.Reader.Ui").ShouldNotContain("Microsoft.NET.ILLink.Tasks");
        Shipped("GoBd.Reader.Ui").ShouldNotContain("Avalonia.BuildServices");
    }

    [Fact]
    public void EveryPackageTheReaderShipsIsNamed()
    {
        var missing = Shipped("GoBd.Reader.Ui").Except(AllNamed()).ToArray();

        missing.ShouldBeEmpty($"THIRD-PARTY-NOTICES.txt names no notice for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryPackageTheValidatorShipsIsNamedForBothTools()
    {
        var both = Named().GetValueOrDefault(BothTools) ?? [];
        var missing = Shipped("GoBd.Validation.Cli").Except(both).ToArray();

        missing.ShouldBeEmpty($"THIRD-PARTY-NOTICES.txt names no notice under '{BothTools}' for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryNamedPackageIsShipped()
    {
        var shipped = Shipped("GoBd.Reader.Ui").Union(Shipped("GoBd.Validation.Cli"));
        var stale = AllNamed().Except(shipped).ToArray();

        stale.ShouldBeEmpty($"THIRD-PARTY-NOTICES.txt names packages no build ships: {string.Join(", ", stale)}");
    }

    [Fact]
    public void WhatIsNamedForBothToolsIsShippedByBoth()
    {
        var both = Named().GetValueOrDefault(BothTools) ?? [];
        var shippedByBoth = Shipped("GoBd.Reader.Ui").Intersect(Shipped("GoBd.Validation.Cli"));
        var wrong = both.Except(shippedByBoth).ToArray();

        wrong.ShouldBeEmpty($"named under '{BothTools}' but not shipped by both tools: {string.Join(", ", wrong)}");
    }
}
