namespace GoBd.Validation.Sources;

/// <summary>
/// One file inside an export, described from the directory listing or the archive's central
/// directory alone — never by reading its content.
/// </summary>
/// <param name="Name">
/// Path relative to the export root, with <c>/</c> separators and no leading slash.
/// </param>
/// <param name="Length">Length in bytes as the listing reports it.</param>
public sealed record ExportEntry(string Name, long Length);
