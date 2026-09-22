using System.Diagnostics.CodeAnalysis;

namespace GoBd.Validation.Sources;

/// <summary>Outcome of resolving a declared <c>URL</c> against the export root.</summary>
public enum UrlResolutionStatus
{
    /// <summary>The value resolved to a name inside the export root.</summary>
    Resolved,

    /// <summary>The value used an absolute form, which the standard forbids.</summary>
    NotRelative,

    /// <summary>The value resolved outside the export root.</summary>
    EscapesRoot,
}

/// <summary>Result of resolving a declared <c>URL</c>.</summary>
/// <param name="Status">How resolution ended.</param>
/// <param name="EntryName">Normalised entry name, set only when <see cref="Status"/> is resolved.</param>
public readonly record struct UrlResolution(UrlResolutionStatus Status, string? EntryName)
{
    /// <summary>True when the value resolved to an entry name inside the root.</summary>
    [MemberNotNullWhen(true, nameof(EntryName))]
    public bool IsResolved => Status == UrlResolutionStatus.Resolved && EntryName is not null;
}

/// <summary>
/// Normalises entry names and resolves the relative <c>URL</c> values the standard permits.
/// </summary>
/// <remarks>
/// The standard allows only relative URLs, explicitly including <c>../</c> segments. Since
/// <c>index.xml</c> sits at the export root, a <c>../</c> that actually ascends leaves the
/// export and is rejected; one that merely doubles back, such as <c>data/../kunden.csv</c>,
/// resolves normally. See design.md D2.
/// </remarks>
public static class ExportPath
{
    /// <summary>Normalises a listing path to the canonical entry-name form.</summary>
    public static string Normalise(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// True when a normalised entry name stays inside the export root.
    /// </summary>
    /// <remarks>
    /// <see cref="Normalise"/> maps <c>\</c> to <c>/</c>, and a backslash is a legal filename
    /// character on Unix — so a file literally named <c>..\outside\secret.dtd</c> normalises to a
    /// name that escapes the root when it is turned back into a path. Entry names are trusted by
    /// everything downstream, so the listing must never contain one that points outside.
    /// </remarks>
    public static bool IsContainedName(string entryName)
    {
        ArgumentNullException.ThrowIfNull(entryName);
        if (entryName.Length == 0 || IsAbsolute(entryName))
        {
            return false;
        }

        foreach (var segment in entryName.Split('/'))
        {
            if (segment == "..")
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Resolves a declared <c>URL</c> relative to the export root.</summary>
    public static UrlResolution Resolve(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (IsAbsolute(url))
        {
            return new UrlResolution(UrlResolutionStatus.NotRelative, null);
        }

        var segments = new List<string>();
        foreach (var segment in url.Replace('\\', '/').Split('/'))
        {
            switch (segment)
            {
                case "":
                case ".":
                    continue;
                case "..":
                    if (segments.Count == 0)
                    {
                        return new UrlResolution(UrlResolutionStatus.EscapesRoot, null);
                    }

                    segments.RemoveAt(segments.Count - 1);
                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        return segments.Count == 0
            ? new UrlResolution(UrlResolutionStatus.EscapesRoot, null)
            : new UrlResolution(UrlResolutionStatus.Resolved, string.Join('/', segments));
    }

    /// <summary>True when the value uses a form the standard rules out.</summary>
    public static bool IsAbsolute(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (url.Length == 0)
        {
            return false;
        }

        // Rooted: /Accounts.dat, \Accounts.dat
        if (url[0] is '/' or '\\')
        {
            return true;
        }

        // Drive-qualified: C:\Accounts.dat, C:/Accounts.dat
        if (url.Length >= 2 && url[1] == ':' && char.IsAsciiLetter(url[0]))
        {
            return true;
        }

        // Scheme-qualified: http://, ftp://, file://localhost/, file:///
        var colon = url.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 1)
        {
            return false;
        }

        // A scheme is letters, digits, '+', '-' and '.', and must start with a letter.
        var scheme = url.AsSpan(0, colon);
        if (!char.IsAsciiLetter(scheme[0]))
        {
            return false;
        }

        foreach (var character in scheme)
        {
            if (!char.IsAsciiLetter(character) && !char.IsAsciiDigit(character)
                && character is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
