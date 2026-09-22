using GoBd.Validation.Cli;

namespace GoBd.Validation.Tests.Cli;

/// <summary>
/// What the executable says about itself: its version, its licence and the notices of the
/// components it contains. The executable is the whole delivery, so nothing beside it can answer
/// these questions for it.
/// </summary>
public sealed class SaysWhatItIsTests
{
    private static readonly string NoSuchExport = Path.Combine(Path.GetTempPath(), $"gobd-none-{Guid.NewGuid():N}.zip");

    [Fact]
    public void TheLicenceIsPrintedInFull()
    {
        var result = CliHarness.Run("--license");

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.ShouldBe(LicenceTexts.Licence);
    }

    [Fact]
    public void WhatTheLicenceDoesNotCoverIsPrintedOnItsOwn()
    {
        // Apart from the licence, as LICENSE and NOTICE are apart in the repository.
        var result = CliHarness.Run("--notice");

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.ShouldBe(LicenceTexts.Notice);
        result.Output.ShouldNotContain("MIT License");
    }

    [Fact]
    public void TheNoticesArePrintedInFull()
    {
        var result = CliHarness.Run("--third-party-notices");

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.ShouldBe(LicenceTexts.ThirdPartyNotices);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("--license")]
    [InlineData("--notice")]
    [InlineData("--third-party-notices")]
    public void AskingWhatItIsValidatesNothing(string option)
    {
        // A path that does not exist would be a tool failure with a report, if it were read.
        var result = CliHarness.Run(NoSuchExport, option);

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.ShouldNotContain("GoBD export validation");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public void TheVersionWinsOverTheLicence() =>
        CliHarness.Run("--version", "--license").Output.TrimEnd().ShouldBe("0.0.0.0");

    [Fact]
    public void TheLicenceWinsOverTheNotices() =>
        CliHarness.Run("--third-party-notices", "--license").Output.ShouldBe(LicenceTexts.Licence);

    [Theory]
    [InlineData("--license")]
    [InlineData("--notice")]
    [InlineData("--third-party-notices")]
    public void TheHelpListsTheOption(string option) =>
        CliHarness.Run("--help").Output.ShouldContain("      " + option);

    [Fact]
    public void AnUntaggedBuildReportsVersionZero()
    {
        // The tests are never built for a release tag, and a build that is not for one must never
        // pass for a release. See the prepare-public-release change's design.md D1.
        var result = CliHarness.Run("--version");

        result.ExitCode.ShouldBe((int)ExitCode.Conformant);
        result.Output.TrimEnd().ShouldBe("0.0.0.0");
    }

    [Fact]
    public void TheVersionIsTheAssemblysOwn() =>
        CliHarness.Run("--version").Output.TrimEnd()
            .ShouldBe(typeof(CliRunner).Assembly.GetName().Version!.ToString());
}
