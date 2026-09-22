using GoBd.Validation.Sources;

namespace GoBd.Validation.Tests.Reader;

/// <summary>
/// A source that holds one entry shut until it is let go.
/// </summary>
/// <remarks>
/// The pipeline's promises are about what happens while a table is still being read, and waiting
/// for a big enough file to be slow enough is how a test becomes flaky. This makes "still being
/// read" a state the test decides rather than one it hopes for.
/// </remarks>
public sealed class HoldingExportSource(IExportSource inner) : IExportSource
{
    private readonly ManualResetEventSlim released = new(false);

    /// <summary>The entry to hold shut until <see cref="Release"/>.</summary>
    public string? Held { get; set; }

    /// <summary>True once the held entry has actually been asked for.</summary>
    public bool Reached { get; private set; }

    /// <summary>Lets the held entry be read.</summary>
    public void Release() => released.Set();

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
        var name = ExportPath.Normalise(entryName);
        if (string.Equals(name, Held, StringComparison.Ordinal))
        {
            Reached = true;
            released.Wait();
        }

        return inner.OpenRead(entryName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        released.Dispose();
        inner.Dispose();
    }
}
