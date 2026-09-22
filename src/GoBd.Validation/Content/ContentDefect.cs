namespace GoBd.Validation.Content;

/// <summary>A way in which a record can fail the layout its table declares.</summary>
/// <remarks>
/// The kinds are the observable defect classes the specification names, not the steps of any
/// one algorithm. Both engines — the streaming reader here and the store the reader UI imports
/// into — express what they found in these terms, which is what lets the two be held to the
/// same findings. See the change's design.md D4.
/// </remarks>
public enum ContentDefectKind
{
    /// <summary>A value does not match the type or format its column declares.</summary>
    TypeOrFormatMismatch,

    /// <summary>A numeric value carries more decimal places than the column's declared accuracy.</summary>
    AccuracyExceeded,

    /// <summary>An alphanumeric value is longer than the column's declared maximum length.</summary>
    MaxLengthExceeded,

    /// <summary>A record yields more or fewer columns than the table declares.</summary>
    ColumnCountMismatch,

    /// <summary>Bytes could not be decoded in the declared codepage.</summary>
    UndecodableBytes,

    /// <summary>A text encapsulator was opened and never closed.</summary>
    UnterminatedEncapsulator,

    /// <summary>A fixed-length record's length differs from the declared length.</summary>
    FixedRecordLengthMismatch,
}

/// <summary>
/// One defect found in one record.
/// </summary>
/// <remarks>
/// A defect carries the offending value and nothing else from the record. A content report is
/// attached to build logs and pasted into tickets, and a record of a GoBD export is a person's
/// data, so what leaves the machine is the cell that is wrong rather than the line it sits in.
/// See the change's design.md D11.
/// </remarks>
/// <param name="Kind">Which defect class this is.</param>
/// <param name="ColumnIndex">Zero-based declared column, or <see langword="null"/> when the defect is the record's.</param>
/// <param name="Value">The offending value, or a count where the defect has no single cell.</param>
/// <param name="Expected">What the declaration required, where saying so is useful.</param>
public sealed record ContentDefect(
    ContentDefectKind Kind,
    int? ColumnIndex,
    string? Value,
    string? Expected = null);
