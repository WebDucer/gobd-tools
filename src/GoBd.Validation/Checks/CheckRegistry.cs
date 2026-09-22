using System.Collections.Immutable;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Checks.Structure;

namespace GoBd.Validation.Checks;

/// <summary>
/// Every check the validator runs, listed explicitly.
/// </summary>
/// <remarks>
/// The list is written out rather than discovered by reflection, so that it survives trimming
/// and NativeAOT and so that adding a check is a visible edit. See design.md D4 and D5.
/// </remarks>
public static class CheckRegistry
{
    /// <summary>The Tier 0 catalogue, in a stable order.</summary>
    public static ImmutableArray<ICheck> Tier0 { get; } =
    [
        // The grammar the export ships.
        new DtdCopyCheck(),

        // Keys and references.
        new ForeignKeyColumnDeclarationCheck(),
        new ReferenceResolutionCheck(),
        new ReferencedPrimaryKeyCheck(),
        new ForeignKeyArityCheck(),
        new ForeignKeyDatatypeCheck(),
        new AliasCheck(),

        // Structure and metadata.
        new MapCheck(),
        new MediaTablesCheck(),
        new FixedRangeCheck(),
        new MetadataLintCheck(),
        new IdentityAmbiguityCheck(),
        new NamingLimitsCheck(),
        new CommandCheck(),
        new ExtensionCheck(),
        new TableFilePresenceCheck(),
    ];

    /// <summary>
    /// The checks that read the data files, run only when the caller asks for them.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Tier0"/> rather than filtered out of it, because the difference
    /// is not a matter of degree: these open every declared data file, and the promise that a
    /// default run does not touch them is worth making structurally.
    /// </remarks>
    public static ImmutableArray<ICheck> Content { get; } =
    [
        new RecordConformanceCheck(),
        new HeaderRowCheck(),
        new KeyIntegrityCheck(),
    ];
}
