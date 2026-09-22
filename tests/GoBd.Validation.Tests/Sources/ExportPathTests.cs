using GoBd.Validation.Sources;
using Shouldly;
using Xunit;

namespace GoBd.Validation.Tests.Sources;

public sealed class ExportPathTests
{
    [Theory]
    [InlineData("Accounts.dat", "Accounts.dat")]
    [InlineData("data/Accounts.dat", "data/Accounts.dat")]
    [InlineData("data/january/Accounts.dat", "data/january/Accounts.dat")]
    [InlineData("./Accounts.dat", "Accounts.dat")]
    [InlineData("data\\Accounts.dat", "data/Accounts.dat")]
    [InlineData("data//Accounts.dat", "data/Accounts.dat")]
    public void RelativeFormsThatStayInsideTheRootResolve(string url, string expected)
    {
        var resolution = ExportPath.Resolve(url);

        resolution.IsResolved.ShouldBeTrue();
        resolution.EntryName.ShouldBe(expected);
    }

    [Fact]
    public void DoublingBackInsideTheRootResolves()
    {
        // The standard lists ../ among the valid relative forms. One that merely doubles back
        // stays inside the export and is fine.
        var resolution = ExportPath.Resolve("data/../kunden.csv");

        resolution.IsResolved.ShouldBeTrue();
        resolution.EntryName.ShouldBe("kunden.csv");
    }

    [Theory]
    [InlineData("http://www.somewhere.com/data/Accounts.dat")]
    [InlineData("ftp://ftp.somewhere.com/data/Accounts.dat")]
    [InlineData("file://localhost/Accounts.dat")]
    [InlineData("file:///Accounts.dat")]
    [InlineData("/Accounts.dat")]
    [InlineData("\\Accounts.dat")]
    [InlineData("C:/Accounts.dat")]
    [InlineData("C:\\Accounts.dat")]
    public void AbsoluteFormsAreRejected(string url)
    {
        // Every one of these is listed as invalid by the standard.
        ExportPath.Resolve(url).Status.ShouldBe(UrlResolutionStatus.NotRelative);
        ExportPath.IsAbsolute(url).ShouldBeTrue();
    }

    [Theory]
    [InlineData("../Accounts.dat")]
    [InlineData("data/../../Accounts.dat")]
    [InlineData("..")]
    public void TraversalOutsideTheRootIsRejected(string url) =>
        // index.xml sits at the export root, so an ascent leaves the export entirely.
        ExportPath.Resolve(url).Status.ShouldBe(UrlResolutionStatus.EscapesRoot);

    [Theory]
    [InlineData("Accounts.dat")]
    [InlineData("data/Accounts.dat")]
    [InlineData("a.b.c")]
    public void OrdinaryNamesAreNotMistakenForAbsoluteForms(string url) =>
        ExportPath.IsAbsolute(url).ShouldBeFalse();

    [Fact]
    public void ResolvingToNothingIsTreatedAsEscaping() =>
        ExportPath.Resolve(".").Status.ShouldBe(UrlResolutionStatus.EscapesRoot);

    [Theory]
    [InlineData("../outside.dtd")]
    [InlineData("data/../../outside.dtd")]
    [InlineData("..")]
    public void AnEntryNameWithAnAscendingSegmentIsNotContained(string entryName) =>
        ExportPath.IsContainedName(entryName).ShouldBeFalse();

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("\\Windows\\win.ini")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("file:///etc/passwd")]
    [InlineData("")]
    public void ARootedEntryNameIsNotContained(string entryName) =>
        ExportPath.IsContainedName(entryName).ShouldBeFalse();

    [Fact]
    public void ANameThatOnlyEscapesAfterNormalisationIsNotContained()
    {
        // A backslash is a legal filename character on Unix, so this is one file sitting inside
        // the export whose name becomes a path out of it. The listing composes the two calls in
        // this order, so the predicate must be judged on the normalised form.
        const string literalName = "..\\outside.dtd";

        ExportPath.IsContainedName(ExportPath.Normalise(literalName)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("index.xml")]
    [InlineData("data/Accounts.dat")]
    [InlineData("data/january/Accounts.dat")]
    [InlineData("..leading.csv")]
    [InlineData("data/..leading.csv")]
    public void OrdinaryEntryNamesAreContained(string entryName) =>
        ExportPath.IsContainedName(entryName).ShouldBeTrue();

    [Theory]
    [InlineData("data\\file.csv", "data/file.csv")]
    [InlineData("/leading.csv", "leading.csv")]
    [InlineData("plain.csv", "plain.csv")]
    public void NormaliseProducesCanonicalEntryNames(string input, string expected) =>
        ExportPath.Normalise(input).ShouldBe(expected);
}
