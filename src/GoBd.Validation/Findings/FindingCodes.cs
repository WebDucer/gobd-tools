using System.Collections.Frozen;
using System.Collections.Immutable;

namespace GoBd.Validation.Findings;

/// <summary>Area a finding code belongs to, encoded in its numeric range.</summary>
public enum FindingCategory
{
    /// <summary>GOBD1xxx — the export source: packaging, entries, URL resolution.</summary>
    Source,

    /// <summary>GOBD2xxx — the DTD and the grammar of <c>index.xml</c>.</summary>
    Grammar,

    /// <summary>GOBD3xxx — semantic coherence of the described data set.</summary>
    Structure,

    /// <summary>GOBD4xxx — the contents of a data file against the layout declared for it.</summary>
    Content,

    /// <summary>GOBD5xxx — declared keys and the references built on them.</summary>
    Integrity,

    /// <summary>GOBD9xxx — the tool could not complete, independent of the export's quality.</summary>
    Tool,
}

/// <summary>One entry in the code catalogue.</summary>
/// <param name="Code">Stable identifier, never reused for a different check.</param>
/// <param name="Category">Area the code belongs to.</param>
/// <param name="DefaultSeverity">Severity the producing check uses unless it escalates.</param>
/// <param name="Summary">Invariant one-line description, for tooling and documentation.</param>
public sealed record FindingCodeInfo(
    string Code,
    FindingCategory Category,
    Severity DefaultSeverity,
    string Summary);

/// <summary>
/// The catalogue of every finding this tool can emit. Codes are the machine interface that
/// downstream pipelines suppress and track by, so they are stable for the life of the tool and
/// a retired code is never reused. See design.md D4.
/// </summary>
public static class FindingCodes
{
    // ---- GOBD1xxx: export source -------------------------------------------------------
    /// <summary>No <c>index.xml</c> at the root of the export.</summary>
    public const string IndexMissing = "GOBD1001";
    /// <summary><c>index.xml</c> exists, but only beneath a subdirectory.</summary>
    public const string IndexNotAtRoot = "GOBD1002";
    /// <summary>A declared <c>Table/URL</c> resolves to no entry in the export.</summary>
    public const string TableFileMissing = "GOBD1003";
    /// <summary>A <c>URL</c> uses an absolute form, which the standard forbids.</summary>
    public const string UrlNotRelative = "GOBD1004";
    /// <summary>A <c>URL</c> resolves outside the export root.</summary>
    public const string UrlEscapesRoot = "GOBD1005";
    /// <summary>An entry matches the declared URL only when case is ignored.</summary>
    public const string EntryCaseMismatch = "GOBD1006";
    /// <summary>Two entries in the export resolve to the same name.</summary>
    public const string EntryNameCollision = "GOBD1007";

    // ---- GOBD2xxx: DTD and grammar -----------------------------------------------------
    /// <summary><c>index.xml</c> is not well formed; no model can be built.</summary>
    public const string XmlNotWellFormed = "GOBD2001";
    /// <summary><c>index.xml</c> violates the Beschreibungsstandard 1.6 grammar.</summary>
    public const string GrammarViolation = "GOBD2002";
    /// <summary>The export ships no DTD file, which the standard requires.</summary>
    public const string DtdMissing = "GOBD2003";
    /// <summary>The shipped DTD differs only in line endings, BOM or trailing whitespace.</summary>
    public const string DtdNormalisationDifference = "GOBD2004";
    /// <summary>The shipped DTD has been modified, which the standard forbids.</summary>
    public const string DtdAltered = "GOBD2005";
    /// <summary>The DOCTYPE names the 2002 grammar; validation used the canonical 1.6 grammar.</summary>
    public const string DtdLegacySystemIdentifier = "GOBD2006";
    /// <summary>The DOCTYPE pointed outside the export; resolution was refused.</summary>
    public const string ExternalReferenceBlocked = "GOBD2007";
    /// <summary>Entity expansion exceeded the permitted budget.</summary>
    public const string EntityExpansionExceeded = "GOBD2008";

    // ---- GOBD3xxx: semantic structure --------------------------------------------------
    /// <summary>A <c>ForeignKey/Name</c> is not declared as a column of its own table.</summary>
    public const string ForeignKeyColumnUndeclared = "GOBD3001";
    /// <summary>A <c>References</c> names no table in the data set.</summary>
    public const string ReferenceDangling = "GOBD3002";
    /// <summary>A <c>References</c> matches more than one table.</summary>
    public const string ReferenceAmbiguous = "GOBD3003";
    /// <summary>The referenced table declares no primary key, so the join has no target.</summary>
    public const string ReferencedTableHasNoPrimaryKey = "GOBD3004";
    /// <summary>Foreign key column count differs from the referenced primary key's.</summary>
    public const string ForeignKeyArityMismatch = "GOBD3005";
    /// <summary>A foreign key column's datatype differs from the primary key column it maps to.</summary>
    public const string ForeignKeyDatatypeMismatch = "GOBD3006";
    /// <summary>An <c>Alias/From</c> is not one of this foreign key's names.</summary>
    public const string AliasFromUnknown = "GOBD3007";
    /// <summary>An <c>Alias/To</c> is not a primary key column of the referenced table.</summary>
    public const string AliasToNotPrimaryKey = "GOBD3008";
    /// <summary>Two aliases in one foreign key map onto the same target column.</summary>
    public const string AliasTargetDuplicated = "GOBD3009";
    /// <summary>A composite key aliases some columns but not others.</summary>
    public const string AliasPartial = "GOBD3010";
    /// <summary>A <c>Map</c> declaring a Time column has unequal From and To masks.</summary>
    public const string TimeMapMasksDiffer = "GOBD3011";
    /// <summary>A <c>Map</c> appears on a column that is not alphanumeric.</summary>
    public const string MapOnNonAlphanumeric = "GOBD3012";
    /// <summary>A <c>Media</c> declares no table and no <c>AcceptNoTables</c>.</summary>
    public const string MediaWithoutTables = "GOBD3013";
    /// <summary>A <c>Media</c> deliberately declares no table.</summary>
    public const string MediaWithoutTablesAccepted = "GOBD3014";
    /// <summary>A <c>FixedRange</c> describes an invalid span.</summary>
    public const string FixedRangeInvalid = "GOBD3015";
    /// <summary>Two fixed-length columns claim overlapping positions.</summary>
    public const string FixedRangeOverlap = "GOBD3016";
    /// <summary>Positions between the first and last declared column are unclaimed.</summary>
    public const string FixedRangeGap = "GOBD3017";
    /// <summary>A fixed-length column extends beyond the declared record length.</summary>
    public const string FixedRangeExceedsRecordLength = "GOBD3018";
    /// <summary>A declared scalar is not a usable non-negative integer.</summary>
    public const string NumericSettingInvalid = "GOBD3019";
    /// <summary>A table declares the same character for decimal and grouping symbols.</summary>
    public const string SymbolCollision = "GOBD3020";
    /// <summary>A table's column, record or encapsulator delimiters collide.</summary>
    public const string DelimiterCollision = "GOBD3021";
    /// <summary>A date mask has no usable year, month or day placeholder, or repeats one.</summary>
    public const string DateMaskUnusable = "GOBD3022";
    /// <summary>A <c>Table/Range/From</c> counts records from below one.</summary>
    public const string RecordRangeInvalid = "GOBD3023";
    /// <summary>Two tables declare the same <c>Name</c>.</summary>
    public const string TableNameDuplicated = "GOBD3024";
    /// <summary>Two tables declare the same <c>URL</c>.</summary>
    public const string TableUrlDuplicated = "GOBD3025";
    /// <summary>Two columns of one table declare the same <c>Name</c>.</summary>
    public const string ColumnNameDuplicated = "GOBD3026";
    /// <summary>A name or descriptive field contains a character the standard forbids.</summary>
    public const string ReservedCharacterInName = "GOBD3027";
    /// <summary>A <c>Description</c> exceeds the documented 255-character limit.</summary>
    public const string DescriptionTooLong = "GOBD3028";
    /// <summary>A <c>Command</c> is declared; it was reported and never executed.</summary>
    public const string CommandDeclared = "GOBD3029";
    /// <summary>An <c>Extension</c> is declared.</summary>
    public const string ExtensionDeclared = "GOBD3030";
    /// <summary>An <c>Extension/URL</c> resolves to no entry in the export.</summary>
    public const string ExtensionFileMissing = "GOBD3031";

    // ---- GOBD4xxx: record conformance ---------------------------------------------------
    /// <summary>A value does not match the type or format its column declares.</summary>
    public const string RecordValueTypeMismatch = "GOBD4001";
    /// <summary>A numeric value carries more decimal places than the declared accuracy.</summary>
    public const string RecordValueAccuracyExceeded = "GOBD4002";
    /// <summary>A value is longer than its column's declared maximum length.</summary>
    public const string RecordValueTooLong = "GOBD4003";
    /// <summary>A record yields a different number of columns than the table declares.</summary>
    public const string RecordColumnCountMismatch = "GOBD4004";
    /// <summary>Bytes could not be decoded in the codepage the table declares.</summary>
    public const string RecordBytesUndecodable = "GOBD4005";
    /// <summary>A text encapsulator was opened and never closed.</summary>
    public const string RecordEncapsulatorUnterminated = "GOBD4006";
    /// <summary>A fixed-length record's length differs from the declared length.</summary>
    public const string RecordLengthMismatch = "GOBD4007";
    /// <summary>Analysis of a table stopped at the configured finding bound.</summary>
    public const string TableAnalysisStopped = "GOBD4008";
    /// <summary>An excluded header row carries the declared names in a different order.</summary>
    public const string HeaderOrderDiffers = "GOBD4009";
    /// <summary>A header row is present but the declaration excludes no record.</summary>
    public const string HeaderUndeclared = "GOBD4010";
    /// <summary>A table's declaration could not be turned into a reading of its file.</summary>
    public const string TableNotReadable = "GOBD4011";

    // ---- GOBD5xxx: keys and referential integrity ---------------------------------------
    /// <summary>A declared primary key value occurs in more than one record.</summary>
    public const string PrimaryKeyDuplicated = "GOBD5001";
    /// <summary>A record's declared primary key is empty, or only partly present.</summary>
    public const string PrimaryKeyIncomplete = "GOBD5002";
    /// <summary>A foreign key value matches no primary key of the referenced table.</summary>
    public const string ForeignKeyValueUnresolved = "GOBD5003";
    /// <summary>A reference could not be checked because the referenced table could not be read.</summary>
    public const string ReferenceNotChecked = "GOBD5004";

    // ---- GOBD9xxx: tool failure --------------------------------------------------------
    /// <summary>The given export path does not exist.</summary>
    public const string ExportPathNotFound = "GOBD9001";
    /// <summary>The export could not be read, or the archive is corrupt.</summary>
    public const string ExportUnreadable = "GOBD9002";
    /// <summary>The requested report language is not available; English was used.</summary>
    public const string LanguageUnsupported = "GOBD9003";
    /// <summary>A check failed unexpectedly; the remaining checks still ran.</summary>
    public const string CheckFailed = "GOBD9004";
    /// <summary>The grammar embedded in this application is not the canonical one.</summary>
    public const string EmbeddedGrammarNotCanonical = "GOBD9005";
    /// <summary>A table holds more keys than this engine can check.</summary>
    public const string KeyCheckCapacityExceeded = "GOBD9006";

    private static readonly ImmutableArray<FindingCodeInfo> AllField =
    [
        new(IndexMissing, FindingCategory.Source, Severity.Error, "index.xml is missing from the export root"),
        new(IndexNotAtRoot, FindingCategory.Source, Severity.Error, "index.xml is not at the export root"),
        new(TableFileMissing, FindingCategory.Source, Severity.Error, "a declared table file is absent"),
        new(UrlNotRelative, FindingCategory.Source, Severity.Error, "a URL is absolute; only relative URLs are permitted"),
        new(UrlEscapesRoot, FindingCategory.Source, Severity.Error, "a URL resolves outside the export root"),
        new(EntryCaseMismatch, FindingCategory.Source, Severity.Error, "an entry matches the declared URL only case-insensitively"),
        new(EntryNameCollision, FindingCategory.Source, Severity.Error, "two entries resolve to the same name"),

        new(XmlNotWellFormed, FindingCategory.Grammar, Severity.Error, "index.xml is not well formed"),
        new(GrammarViolation, FindingCategory.Grammar, Severity.Error, "index.xml violates the 1.6 grammar"),
        new(DtdMissing, FindingCategory.Grammar, Severity.Error, "the export ships no DTD file"),
        new(DtdNormalisationDifference, FindingCategory.Grammar, Severity.Warning, "the shipped DTD differs only in encoding artefacts"),
        new(DtdAltered, FindingCategory.Grammar, Severity.Error, "the shipped DTD has been modified"),
        new(DtdLegacySystemIdentifier, FindingCategory.Grammar, Severity.Info, "the DOCTYPE names the 2002 grammar"),
        new(ExternalReferenceBlocked, FindingCategory.Grammar, Severity.Error, "an external DOCTYPE reference was refused"),
        new(EntityExpansionExceeded, FindingCategory.Grammar, Severity.Error, "entity expansion exceeded the permitted budget"),

        new(ForeignKeyColumnUndeclared, FindingCategory.Structure, Severity.Error, "a foreign key names an undeclared column"),
        new(ReferenceDangling, FindingCategory.Structure, Severity.Error, "a reference names no table in the data set"),
        new(ReferenceAmbiguous, FindingCategory.Structure, Severity.Error, "a reference matches more than one table"),
        new(ReferencedTableHasNoPrimaryKey, FindingCategory.Structure, Severity.Error, "the referenced table declares no primary key"),
        new(ForeignKeyArityMismatch, FindingCategory.Structure, Severity.Error, "foreign key arity differs from the referenced primary key"),
        new(ForeignKeyDatatypeMismatch, FindingCategory.Structure, Severity.Error, "a foreign key column's datatype differs from its target"),
        new(AliasFromUnknown, FindingCategory.Structure, Severity.Error, "an alias maps from a column outside its foreign key"),
        new(AliasToNotPrimaryKey, FindingCategory.Structure, Severity.Error, "an alias maps onto a non-key column"),
        new(AliasTargetDuplicated, FindingCategory.Structure, Severity.Error, "two aliases map onto the same target column"),
        new(AliasPartial, FindingCategory.Structure, Severity.Warning, "a composite key is only partially aliased"),
        new(TimeMapMasksDiffer, FindingCategory.Structure, Severity.Warning, "a time map's From and To masks differ"),
        new(MapOnNonAlphanumeric, FindingCategory.Structure, Severity.Error, "a map appears on a non-alphanumeric column"),
        new(MediaWithoutTables, FindingCategory.Structure, Severity.Warning, "a medium describes no data"),
        new(MediaWithoutTablesAccepted, FindingCategory.Structure, Severity.Info, "a medium deliberately describes no data"),
        new(FixedRangeInvalid, FindingCategory.Structure, Severity.Error, "a fixed range describes an invalid span"),
        new(FixedRangeOverlap, FindingCategory.Structure, Severity.Warning, "two fixed-length columns overlap"),
        new(FixedRangeGap, FindingCategory.Structure, Severity.Info, "a fixed-length record leaves unclaimed positions"),
        new(FixedRangeExceedsRecordLength, FindingCategory.Structure, Severity.Error, "a column extends beyond the declared record length"),
        new(NumericSettingInvalid, FindingCategory.Structure, Severity.Error, "a declared scalar is not a usable non-negative integer"),
        new(SymbolCollision, FindingCategory.Structure, Severity.Error, "decimal and grouping symbols are identical"),
        new(DelimiterCollision, FindingCategory.Structure, Severity.Error, "declared delimiters collide"),
        new(DateMaskUnusable, FindingCategory.Structure, Severity.Error, "a date mask is unusable"),
        new(RecordRangeInvalid, FindingCategory.Structure, Severity.Error, "a record range starts below one"),
        new(TableNameDuplicated, FindingCategory.Structure, Severity.Warning, "two tables share a name"),
        new(TableUrlDuplicated, FindingCategory.Structure, Severity.Warning, "two tables share a URL"),
        new(ColumnNameDuplicated, FindingCategory.Structure, Severity.Error, "two columns of one table share a name"),
        new(ReservedCharacterInName, FindingCategory.Structure, Severity.Warning, "a name contains a character the standard forbids"),
        new(DescriptionTooLong, FindingCategory.Structure, Severity.Info, "a description exceeds 255 characters"),
        new(CommandDeclared, FindingCategory.Structure, Severity.Warning, "a Command is declared and was not executed"),
        new(ExtensionDeclared, FindingCategory.Structure, Severity.Info, "an Extension is declared"),
        new(ExtensionFileMissing, FindingCategory.Structure, Severity.Error, "an Extension URL resolves to no entry"),

        new(RecordValueTypeMismatch, FindingCategory.Content, Severity.Error, "a value does not match its column's declared type or format"),
        new(RecordValueAccuracyExceeded, FindingCategory.Content, Severity.Error, "a value carries more decimal places than the declared accuracy"),
        new(RecordValueTooLong, FindingCategory.Content, Severity.Error, "a value exceeds its column's declared maximum length"),
        new(RecordColumnCountMismatch, FindingCategory.Content, Severity.Error, "a record yields a different number of columns than declared"),
        new(RecordBytesUndecodable, FindingCategory.Content, Severity.Error, "bytes could not be decoded in the declared codepage"),
        new(RecordEncapsulatorUnterminated, FindingCategory.Content, Severity.Error, "a text encapsulator was never closed"),
        new(RecordLengthMismatch, FindingCategory.Content, Severity.Error, "a fixed-length record's length differs from the declared length"),
        new(TableAnalysisStopped, FindingCategory.Content, Severity.Info, "analysis of a table stopped at the finding bound"),
        new(HeaderOrderDiffers, FindingCategory.Content, Severity.Error, "a header row's column order differs from the declaration"),
        new(HeaderUndeclared, FindingCategory.Content, Severity.Error, "a header row is present but no record is excluded"),
        new(TableNotReadable, FindingCategory.Content, Severity.Error, "a table's declaration could not be turned into a reading"),

        new(PrimaryKeyDuplicated, FindingCategory.Integrity, Severity.Error, "a primary key value occurs more than once"),
        new(PrimaryKeyIncomplete, FindingCategory.Integrity, Severity.Error, "a primary key is empty or only partly present"),
        new(ForeignKeyValueUnresolved, FindingCategory.Integrity, Severity.Error, "a foreign key value matches no record of the referenced table"),
        new(ReferenceNotChecked, FindingCategory.Integrity, Severity.Warning, "a reference could not be checked"),

        new(ExportPathNotFound, FindingCategory.Tool, Severity.Error, "the export path does not exist"),
        new(ExportUnreadable, FindingCategory.Tool, Severity.Error, "the export could not be read"),
        new(LanguageUnsupported, FindingCategory.Tool, Severity.Info, "the requested report language is unavailable"),
        new(CheckFailed, FindingCategory.Tool, Severity.Error, "a check failed unexpectedly"),
        new(EmbeddedGrammarNotCanonical, FindingCategory.Tool, Severity.Error, "the embedded canonical grammar failed its self-check"),
        new(KeyCheckCapacityExceeded, FindingCategory.Tool, Severity.Error, "a table holds more keys than this engine can check"),
    ];

    private static readonly FrozenDictionary<string, FindingCodeInfo> ByCode =
        AllField.ToFrozenDictionary(info => info.Code, StringComparer.Ordinal);

    /// <summary>Every code this tool can emit.</summary>
    public static ImmutableArray<FindingCodeInfo> All => AllField;

    /// <summary>Looks up a code's catalogue entry.</summary>
    public static FindingCodeInfo Describe(string code) =>
        ByCode.TryGetValue(code, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(code), code, "Code is not in the catalogue.");

    /// <summary>The severity a code carries unless its check deliberately escalates.</summary>
    public static Severity SeverityOf(string code) => Describe(code).DefaultSeverity;

    /// <summary>True when the code is known to the catalogue.</summary>
    public static bool IsKnown(string code) => ByCode.ContainsKey(code);
}
