using GoBd.Validation.Sources;

namespace GoBd.Validation.Tests.Sources;

/// <summary>
/// Wraps a source and records which entries were actually opened, so a test can assert that a
/// data file was never read rather than merely that the run was fast.
/// </summary>
public sealed class CountingExportSource(IExportSource inner) : IExportSource
{
    private readonly List<string> opened = [];

    /// <summary>Entry names passed to <see cref="OpenRead"/>, in call order.</summary>
    public IReadOnlyList<string> Opened => opened;

    /// <inheritdoc />
    public string Location => inner.Location;

    /// <inheritdoc />
    public IReadOnlyList<ExportEntry> Entries => inner.Entries;

    /// <inheritdoc />
    public ExportEntry? Find(string entryName) => inner.Find(entryName);

    /// <inheritdoc />
    public IReadOnlyList<ExportEntry> FindCaseInsensitive(string entryName) => inner.FindCaseInsensitive(entryName);

    /// <inheritdoc />
    public IReadOnlyList<(string Name, int Count)> NameCollisions => inner.NameCollisions;

    /// <inheritdoc />
    public Stream OpenRead(string entryName)
    {
        opened.Add(ExportPath.Normalise(entryName));
        return inner.OpenRead(entryName);
    }

    /// <inheritdoc />
    public void Dispose() => inner.Dispose();
}
