using System.Reflection;

namespace GoBd.Validation;

/// <summary>
/// The licence both tools are distributed under, and the notices of the components they contain.
/// </summary>
/// <remarks>
/// Each tool reaches a person as one file with nothing beside it, so whatever the licences of
/// its parts require it to carry, it carries inside itself: the validator prints these texts and
/// the reader shows them. They are the repository's own <c>LICENSE</c>, <c>NOTICE</c> and
/// <c>THIRD-PARTY-NOTICES.txt</c>, embedded as they stand, and kept apart as the files are: the
/// licence is the MIT licence and nothing else, as GitHub reads it. See the prepare-public-release
/// change's design.md D2.
/// </remarks>
public static class LicenceTexts
{
    /// <summary>The licence the tools are distributed under: <c>LICENSE</c>, the MIT licence.</summary>
    public static string Licence => Read("GoBd.LICENSE");

    /// <summary>What that licence does not cover: <c>NOTICE</c>.</summary>
    public static string Notice => Read("GoBd.NOTICE");

    /// <summary>The notices of every third-party component either tool contains.</summary>
    public static string ThirdPartyNotices => Read("GoBd.THIRD-PARTY-NOTICES.txt");

    /// <summary>The copyright line of the licence, as the licence states it.</summary>
    /// <remarks>
    /// Taken from the licence rather than written out again, so that what the reader shows and
    /// what <c>LICENSE</c> says cannot come apart.
    /// </remarks>
    public static string Copyright =>
        Read("GoBd.LICENSE")
            .Split('\n')
            .Select(line => line.Trim())
            .First(line => line.StartsWith("Copyright ", StringComparison.Ordinal));

    private static string Read(string resourceName)
    {
        using var stream = typeof(LicenceTexts).GetTypeInfo().Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
