using GoBd.Validation.Collections;

namespace GoBd.Validation.Findings;

/// <summary>
/// One validation outcome.
/// </summary>
/// <remarks>
/// A finding carries its stable <see cref="Code"/> and the <see cref="Arguments"/> that
/// distinguish this occurrence from another under the same code — never a rendered sentence.
/// Message text is produced from the code and arguments at render time, which is what keeps a
/// finding's machine identity identical in English and German. See design.md D8.
/// <para>
/// Equality is the record's own compiler-generated implementation. That works because
/// <see cref="Arguments"/> is an <see cref="EquatableArray{T}"/> rather than an
/// <c>ImmutableArray</c>, whose equality would be by underlying reference.
/// </para>
/// </remarks>
public sealed record Finding
{
    /// <summary>Creates a finding, taking its severity from the code catalogue.</summary>
    public static Finding Create(string code, SourceLocation? location, params string[] arguments) =>
        new(code, FindingCodes.SeverityOf(code), location, arguments);

    /// <summary>Creates a finding whose severity overrides the catalogue default.</summary>
    public static Finding Create(string code, Severity severity, SourceLocation? location, params string[] arguments) =>
        new(code, severity, location, arguments);

    /// <summary>Returns this finding attributed to what it concerns.</summary>
    public Finding About(FindingScope scope) => this with { Scope = scope };

    private Finding(string code, Severity severity, SourceLocation? location, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentNullException.ThrowIfNull(arguments);
        Code = code;
        Severity = severity;
        Location = location;
        Arguments = new EquatableArray<string>(arguments);
    }

    /// <summary>Stable, namespaced identifier for the check that produced this finding.</summary>
    public string Code { get; }

    /// <summary>What this finding says about conformance.</summary>
    public Severity Severity { get; }

    /// <summary>
    /// Where in <c>index.xml</c> this finding arose, or <see langword="null"/> when it concerns
    /// the export as a whole and no position would be truthful.
    /// </summary>
    public SourceLocation? Location { get; }

    /// <summary>Values that distinguish this occurrence, substituted into the rendered message.</summary>
    public EquatableArray<string> Arguments { get; }

    /// <summary>
    /// What this finding concerns, or <see langword="null"/> when it concerns the export as a
    /// whole. Stated by the producing check rather than recovered from argument order.
    /// </summary>
    public FindingScope? Scope { get; init; }
}
