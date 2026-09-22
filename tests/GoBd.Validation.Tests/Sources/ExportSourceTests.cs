using System.IO.Compression;
using GoBd.Validation.Sources;
using GoBd.Validation.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Sources;

/// <summary>
/// The same assertions are run against both implementations, because every downstream check
/// must behave identically whether the export arrived zipped or unpacked.
/// </summary>
public sealed class ExportSourceTests
{
    public static TheoryData<bool> BothSourceKinds => new() { true, false };

    private static IExportSource Open(bool zipped, out string path)
    {
        path = zipped
            ? FixtureLibrary.ZipPath(FixtureLibrary.GoodExport)
            : FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);
        return zipped ? new ZipExportSource(path) : new FolderExportSource(path);
    }

    private static void WithSource(bool zipped, Action<IExportSource> assert)
    {
        var source = Open(zipped, out var path);
        try
        {
            assert(source);
        }
        finally
        {
            source.Dispose();
            if (zipped)
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void LocatesIndexXmlAtTheRoot(bool zipped) =>
        WithSource(zipped, source => source.Find("index.xml").ShouldNotBeNull());

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void EnumeratesNestedEntriesWithForwardSlashes(bool zipped) =>
        WithSource(zipped, source =>
            source.Entries.Select(entry => entry.Name)
                .ShouldContain("data/bestellungen.csv"));

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void EntriesAreOrderedByOrdinalName(bool zipped) =>
        WithSource(zipped, source =>
            source.Entries.Select(entry => entry.Name)
                .ShouldBe(source.Entries.Select(entry => entry.Name).OrderBy(name => name, StringComparer.Ordinal)));

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void MatchingIsCaseSensitive(bool zipped) =>
        WithSource(zipped, source => source.Find("Index.xml").ShouldBeNull());

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void CaseOnlyDifferencesAreReportableSeparately(bool zipped) =>
        WithSource(zipped, source =>
            source.FindCaseInsensitive("KUNDEN.CSV")
                .Select(entry => entry.Name)
                .ShouldBe(["kunden.csv"]));

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void CaseInsensitiveLookupExcludesAnExactMatch(bool zipped) =>
        WithSource(zipped, source => source.FindCaseInsensitive("kunden.csv").ShouldBeEmpty());

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void EntryLengthsComeFromTheListing(bool zipped) =>
        WithSource(zipped, source => source.Find("kunden.csv")!.Length.ShouldBeGreaterThan(0));

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void OpenReadYieldsTheEntryContent(bool zipped) =>
        WithSource(zipped, source =>
        {
            using var stream = source.OpenRead("index.xml");
            using var reader = new StreamReader(stream);
            reader.ReadToEnd().ShouldContain("<DataSet>");
        });

    [Theory]
    [MemberData(nameof(BothSourceKinds))]
    public void OpeningAnAbsentEntryThrows(bool zipped) =>
        WithSource(zipped, source =>
            Should.Throw<FileNotFoundException>(() => source.OpenRead("nope.csv")));

    [Fact]
    public void ZipAndFolderProduceIdenticalEntrySets()
    {
        using var folder = new FolderExportSource(FixtureLibrary.FolderPath(FixtureLibrary.GoodExport));
        var zipPath = FixtureLibrary.ZipPath(FixtureLibrary.GoodExport);
        try
        {
            using var zip = new ZipExportSource(zipPath);
            zip.Entries.Select(entry => entry.Name)
                .ShouldBe(folder.Entries.Select(entry => entry.Name));
        }
        finally
        {
            File.Delete(zipPath);
        }
    }

    /// <summary>
    /// Shell completion writes a folder with a trailing separator, so that is a path the tool is
    /// handed routinely. It used to refuse every entry, index.xml included, because the root it
    /// compared against kept the separator and nothing starts with `export//`.
    /// </summary>
    [Fact]
    public void AFolderNamedWithATrailingSeparatorIsTheSameExport()
    {
        var path = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport);
        using var plain = new FolderExportSource(path);
        using var trailing = new FolderExportSource(path + Path.DirectorySeparatorChar);

        trailing.Entries.Select(entry => entry.Name).ShouldBe(plain.Entries.Select(entry => entry.Name));
        trailing.Find("index.xml").ShouldNotBeNull();
    }

    [Fact]
    public void AnEntryOfAFolderNamedWithATrailingSeparatorCanBeOpened()
    {
        var path = FixtureLibrary.FolderPath(FixtureLibrary.GoodExport) + Path.DirectorySeparatorChar;
        using var source = new FolderExportSource(path);

        using var stream = source.OpenRead("index.xml");
        using var reader = new StreamReader(stream);
        reader.ReadToEnd().ShouldContain("<DataSet>");
    }
}

/// <summary>
/// The export listing is the boundary that keeps an untrusted medium from reaching the rest of
/// the filesystem, so these assert what it must never let through.
/// </summary>
public sealed class ExportEntryIndexTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"gobd-idx-{Guid.NewGuid():N}");

    public ExportEntryIndexTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void NamesCollidingAfterNormalisationDoNotCrashTheSource()
    {
        // A backslash is a legal filename character on Unix, so these two distinct files both
        // normalise to "data/x.csv". The folder source used to build its lookup with
        // ToDictionary and threw ArgumentException, which ExportSourceFactory does not catch --
        // an unhandled crash instead of a finding. The ZIP source silently accepted the same
        // input, so the two disagreed as well.
        Directory.CreateDirectory(Path.Combine(directory, "data"));
        File.WriteAllText(Path.Combine(directory, "data", "x.csv"), "a");
        File.WriteAllText(Path.Combine(directory, "data" + '\\' + "x.csv"), "b");

        using var source = Should.NotThrow(() => new FolderExportSource(directory));

        source.Find("data/x.csv").ShouldNotBeNull();
    }

    [Fact]
    public void CollidingEntryNamesAreReportedRatherThanResolvedSilently()
    {
        // Both files sit inside the export, but they normalise to one name. Picking one quietly
        // means two importers can reach two conclusions from the same medium.
        Directory.CreateDirectory(Path.Combine(directory, "data"));
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
        File.WriteAllText(Path.Combine(directory, "data", "x.csv"), "a");
        File.WriteAllText(Path.Combine(directory, "data" + '\\' + "x.csv"), "b");

        using var source = new FolderExportSource(directory);

        var collision = source.NameCollisions.ShouldHaveSingleItem();
        collision.Name.ShouldBe("data/x.csv");
        collision.Count.ShouldBe(2);
    }

    [Fact]
    public void ADuplicateZipMemberIsReported()
    {
        // Duplicate member names are legal in the ZIP format and a known spoofing vector.
        var archive = Path.Combine(Path.GetTempPath(), $"gobd-dup-{Guid.NewGuid():N}.zip");
        try
        {
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (var body in (string[])["first", "second"])
                {
                    using var writer = new StreamWriter(zip.CreateEntry("gdpdu-01-03-2019.dtd").Open());
                    writer.Write(body);
                }
            }

            using var source = new ZipExportSource(archive);

            source.NameCollisions.ShouldHaveSingleItem().Count.ShouldBe(2);
        }
        finally
        {
            File.Delete(archive);
        }
    }

    [Fact]
    public void AnExportWithoutCollisionsReportsNone()
    {
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
        File.WriteAllText(Path.Combine(directory, "kunden.csv"), "a");

        using var source = new FolderExportSource(directory);

        source.NameCollisions.ShouldBeEmpty();
    }

    [Fact]
    public void ANameThatEscapesTheRootIsNotListed()
    {
        // A backslash is a legal filename character on Unix, so this single file inside the
        // export normalises to a name pointing outside it. Left in the listing, DtdCopyLocator
        // would open it and publish its SHA-256 -- a hash oracle for arbitrary files on the
        // machine running the validator.
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
        File.WriteAllText(Path.Combine(directory, ".." + '\\' + "outside.dtd"), "secret");

        using var source = new FolderExportSource(directory);

        source.Entries.Select(entry => entry.Name).ShouldBe(["index.xml"]);
        source.Find("../outside.dtd").ShouldBeNull();
    }

    [Fact]
    public void ASymbolicLinkIsNotListed()
    {
        // Needs no trickery and works on any platform: a link named like the DTD would make the
        // validator read and hash whatever it points at.
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
        var target = Path.Combine(Path.GetTempPath(), $"gobd-target-{Guid.NewGuid():N}");
        File.WriteAllText(target, "secret");
        try
        {
            File.CreateSymbolicLink(Path.Combine(directory, "gdpdu-01-03-2019.dtd"), target);

            using var source = new FolderExportSource(directory);

            source.Entries.Select(entry => entry.Name).ShouldBe(["index.xml"]);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void OpeningAnEntryIsConfinedToTheExportRootEvenIfOneReachesTheIndex()
    {
        // The read is refused, which is what matters. Note which barrier does it: the listing
        // excluded the name, so Find refuses before OpenRead's containment check is reached.
        // That check is unreachable from outside by construction -- which is the point of it --
        // so this asserts the refusal, and the confinement itself stands as defence in depth
        // for any future path that puts a name into the index.
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");

        using var source = new FolderExportSource(directory);

        Should.Throw<FileNotFoundException>(() => source.OpenRead("../outside.dtd"));
    }

    [Fact]
    public void AZipEntryNamedToEscapeTheRootIsNotListed()
    {
        var archive = Path.Combine(Path.GetTempPath(), $"gobd-slip-{Guid.NewGuid():N}.zip");
        try
        {
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("index.xml").Open()))
                {
                    writer.Write("<DataSet/>");
                }

                using (var writer = new StreamWriter(zip.CreateEntry("../escaped.dtd").Open()))
                {
                    writer.Write("secret");
                }
            }

            using var source = new ZipExportSource(archive);

            source.Entries.Select(entry => entry.Name).ShouldBe(["index.xml"]);
        }
        finally
        {
            File.Delete(archive);
        }
    }

    [Fact]
    public void AFileUnderASymbolicallyLinkedDirectoryIsNotListed()
    {
        // The files under a linked directory are not links themselves, so a per-file link check
        // never sees them: they arrive with ordinary in-root names and pass every other test.
        // A directory link named data is all it takes to make the validator hash /etc.
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
        var outside = Path.Combine(Path.GetTempPath(), $"gobd-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "gdpdu-01-03-2019.dtd"), "secret");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(directory, "data"), outside);

            using var source = new FolderExportSource(directory);

            source.Entries.Select(entry => entry.Name).ShouldBe(["index.xml"]);
            source.Find("data/gdpdu-01-03-2019.dtd").ShouldBeNull();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void AnOrdinaryNestedFileIsStillListed()
    {
        // The guard above narrows the enumeration, so this holds it to excluding only links.
        File.WriteAllText(Path.Combine(directory, "index.xml"), "<DataSet/>");
        Directory.CreateDirectory(Path.Combine(directory, "data"));
        File.WriteAllText(Path.Combine(directory, "data", "kunden.csv"), "a");
        File.WriteAllText(Path.Combine(directory, ".hidden"), "a");

        using var source = new FolderExportSource(directory);

        source.Entries.Select(entry => entry.Name)
            .ShouldBe([".hidden", "data/kunden.csv", "index.xml"]);
    }

    [Fact]
    public void TheEntryThatIsListedIsTheEntryThatIsOpened()
    {
        // Which of two colliding files wins depends on the order the filesystem enumerated in,
        // so this asserts they agree rather than which one won. Rebuilding the path from the
        // normalised name broke that: the lookup returned the one-byte file literally named
        // data\x.csv while the open read the three-byte data/x.csv beside it, so the length a
        // check saw and the bytes it hashed came from different files.
        Directory.CreateDirectory(Path.Combine(directory, "data"));
        File.WriteAllText(Path.Combine(directory, "data", "x.csv"), "aaa");
        File.WriteAllText(Path.Combine(directory, "data" + '\\' + "x.csv"), "b");

        using var source = new FolderExportSource(directory);

        var entry = source.Find("data/x.csv").ShouldNotBeNull();
        using var reader = new StreamReader(source.OpenRead("data/x.csv"));
        reader.ReadToEnd().Length.ShouldBe((int)entry.Length);
    }

    [Fact]
    public void ADuplicateZipMemberIsReadAsTheOneThatWasListed()
    {
        // Archive order decides, and it is the same order the listing reported, so the member
        // whose hash reaches the report is the member the listing named.
        var archive = Path.Combine(Path.GetTempPath(), $"gobd-first-{Guid.NewGuid():N}.zip");
        try
        {
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (var body in (string[])["first", "second-and-longer"])
                {
                    using var writer = new StreamWriter(zip.CreateEntry("gdpdu-01-03-2019.dtd").Open());
                    writer.Write(body);
                }
            }

            using var source = new ZipExportSource(archive);

            var entry = source.Find("gdpdu-01-03-2019.dtd").ShouldNotBeNull();
            entry.Length.ShouldBe("first".Length);
            using var reader = new StreamReader(source.OpenRead("gdpdu-01-03-2019.dtd"));
            reader.ReadToEnd().ShouldBe("first");
        }
        finally
        {
            File.Delete(archive);
        }
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
