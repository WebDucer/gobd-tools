using GoBd.Validation.Content;
using GoBd.Validation.Model;
using GoBd.Validation.Sources;

namespace GoBd.Validation.Checks.Content;

/// <summary>Why a declared table's contents can or cannot be read.</summary>
public enum TableDataStatus
{
    /// <summary>The file is present and the declaration yields a reading of it.</summary>
    Ready,

    /// <summary>The declared URL does not resolve, or no entry carries that name.</summary>
    FileAbsent,

    /// <summary>The file is present, but the declaration does not say how to read it.</summary>
    LayoutUnusable,
}

/// <summary>A declared table's data file and the layout it is read under.</summary>
/// <param name="Status">Whether the contents can be read, and why not when they cannot.</param>
/// <param name="Layout">The layout resolved from the declaration.</param>
/// <param name="EntryName">The entry holding the data, when there is one.</param>
public readonly record struct TableData(TableDataStatus Status, RecordLayout Layout, string EntryName)
{
    /// <summary>True when the contents can be read.</summary>
    public bool IsReady => Status == TableDataStatus.Ready;

    /// <summary>
    /// Locates a table's data file and layout.
    /// </summary>
    /// <remarks>
    /// An absent file is not reported here. <c>GOBD1003</c> and its neighbours already say the
    /// declared URL resolves to nothing, and a content code repeating it would make a report
    /// longer without making it more useful. An unusable layout is different: the file is there
    /// and nothing else says why it was not read.
    /// </remarks>
    public static TableData For(CheckContext context, TableNode table)
    {
        ArgumentNullException.ThrowIfNull(context);
        return For(context.Source, table);
    }

    /// <summary>Locates a table's data file and layout in the given export.</summary>
    public static TableData For(IExportSource source, TableNode table)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(table);

        var layout = RecordLayout.For(table);
        var resolution = ExportPath.Resolve(table.Url.Value);
        if (!resolution.IsResolved || source.Find(resolution.EntryName) is null)
        {
            return new TableData(TableDataStatus.FileAbsent, layout, string.Empty);
        }

        return layout.IsReadable
            ? new TableData(TableDataStatus.Ready, layout, resolution.EntryName)
            : new TableData(TableDataStatus.LayoutUnusable, layout, resolution.EntryName);
    }
}
