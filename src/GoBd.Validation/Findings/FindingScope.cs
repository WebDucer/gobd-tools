namespace GoBd.Validation.Findings;

/// <summary>What kind of thing a finding is about.</summary>
public enum FindingScopeKind
{
    /// <summary>The export as its <c>DataSet</c> element describes it.</summary>
    Export,

    /// <summary>A file the medium carries, named as the listing names it.</summary>
    Entry,

    /// <summary>A table described in <c>index.xml</c>.</summary>
    Table,

    /// <summary>A medium described in <c>index.xml</c>.</summary>
    Medium,

    /// <summary>A declared extension.</summary>
    Extension,

    /// <summary>The DTD the export ships.</summary>
    Dtd,
}

/// <summary>
/// The thing a finding concerns, stated by the check that produced it.
/// </summary>
/// <remarks>
/// Previously the reporter recovered this by indexing into a finding's positional arguments,
/// with the index declared per code in the catalogue. That coupled a published JSON field to
/// argument order: reordering a check's arguments to improve its wording silently changed the
/// reported scope, and nothing — no compiler check, no test — noticed. The check knows exactly
/// what its finding is about, so it says so.
/// </remarks>
/// <param name="Kind">What sort of thing <paramref name="Name"/> names.</param>
/// <param name="Name">Its identity, as the document declares it.</param>
public sealed record FindingScope(FindingScopeKind Kind, string Name)
{
    /// <summary>A finding about a table.</summary>
    public static FindingScope Table(string identity) => new(FindingScopeKind.Table, identity);

    /// <summary>A finding about a medium.</summary>
    public static FindingScope Medium(string name) => new(FindingScopeKind.Medium, name);

    /// <summary>A finding about a declared extension.</summary>
    public static FindingScope Extension(string name) => new(FindingScopeKind.Extension, name);

    /// <summary>A finding about the DTD the export ships.</summary>
    public static FindingScope Dtd(string fileName) => new(FindingScopeKind.Dtd, fileName);

    /// <summary>A finding about the export as its <c>DataSet</c> element describes it.</summary>
    public static FindingScope Export(string name) => new(FindingScopeKind.Export, name);

    /// <summary>A finding about one entry in the medium, such as a colliding name.</summary>
    public static FindingScope Entry(string entryName) => new(FindingScopeKind.Entry, entryName);
}
