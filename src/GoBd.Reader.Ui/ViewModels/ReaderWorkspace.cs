using GoBd.Reader.Data;
using GoBd.Validation.Checks.Content;
using GoBd.Validation.Findings;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>How asking to open an export ended.</summary>
public enum OpenOutcome
{
    /// <summary>The export is open, and the previous one, if any, has been closed.</summary>
    Opened,

    /// <summary>Nothing was chosen, so nothing changed.</summary>
    Dismissed,

    /// <summary>What was chosen could not be opened; whatever was open stays open.</summary>
    Refused,
}

/// <summary>What asking to open an export produced.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Findings">What was found while opening, or why it could not be opened.</param>
/// <param name="Refusal">Why the store would not open, when that was the reason.</param>
public sealed record WorkspaceOpenResult(
    OpenOutcome Outcome,
    IReadOnlyList<Finding> Findings,
    StoreRefusal? Refusal);

/// <summary>
/// The one export the reader has open, and the opening of the next.
/// </summary>
/// <remarks>
/// The command line and the picker both arrive here, so a scripted run and a person choosing a
/// medium reach the same state. A path is normalised on the way in because a platform picker may
/// hand a folder back with a trailing separator, and the same folder must not open differently for
/// having been chosen rather than typed.
/// </remarks>
/// <param name="options">Where stores may put their files.</param>
/// <param name="bound">Findings per table after which analysis stops.</param>
public sealed class ReaderWorkspace(
    StoreOptions? options = null,
    int bound = ContentOptions.DefaultMaximumFindingsPerTable) : IDisposable
{
    private CancellationTokenSource? cancellation;

    /// <summary>The open export, if there is one.</summary>
    public ReaderSession? Session { get; private set; }

    /// <summary>Where the open export was opened from, normalised.</summary>
    public string? ExportPath { get; private set; }

    /// <summary>
    /// Reading the open export, which runs on a worker until it finishes or is cancelled.
    /// </summary>
    /// <remarks>
    /// Exposed rather than awaited internally, because the window must not wait for it: staying
    /// responsive while the export is read is the whole point. A caller that does need the
    /// export read — a test, the close path — waits on this.
    /// </remarks>
    public Task Reading { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Opens an export, replacing the one that was open, and starts reading it.
    /// </summary>
    /// <remarks>
    /// The new export is opened before the old one is closed, so that choosing something that is
    /// not an export costs nothing: the reader keeps what it had and says why. Once the new one
    /// is open, the old one's reading is cancelled and waited for, and only then is the old
    /// session closed and its store released — a reader that accumulated sessions would
    /// accumulate stores with them, and disposing a session a worker is still importing into
    /// would pull the store out from under it.
    /// </remarks>
    /// <param name="exportPath">A ZIP archive or a folder, or null when the choice was dismissed.</param>
    /// <param name="progress">Told how far reading has got, on the worker's thread.</param>
    public WorkspaceOpenResult Open(string? exportPath, IProgress<ExportReading>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            return new WorkspaceOpenResult(OpenOutcome.Dismissed, [], null);
        }

        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(exportPath));
        var result = ReaderSession.Open(path, options, bound);
        if (result.Session is not { } opened)
        {
            return new WorkspaceOpenResult(OpenOutcome.Refused, result.Findings, result.Refusal);
        }

        Close();

        var token = new CancellationTokenSource();
        cancellation = token;
        Session = opened;
        ExportPath = path;

        // Long-running rather than a pooled work item: this occupies its thread for as long as
        // the export takes to read, which on a multi-gigabyte medium is minutes.
        Reading = Task.Factory.StartNew(
            () => opened.Read(progress, token.Token),
            token.Token,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);

        return new WorkspaceOpenResult(OpenOutcome.Opened, result.Findings, null);
    }

    /// <summary>Closes the open export and removes its store.</summary>
    public void Dispose()
    {
        Close();
        cancellation = null;
    }

    /// <summary>
    /// Stops reading and closes whatever was open.
    /// </summary>
    /// <remarks>
    /// The order is the point. Cancelling stops extraction between records; interrupting stops a
    /// statement already inside the store, which a token cannot reach. Only once the worker has
    /// actually stopped is the session disposed, because disposing it deletes the store the
    /// worker is writing into.
    /// </remarks>
    private void Close()
    {
        var previous = Session;
        var token = cancellation;
        var reading = Reading;

        Session = null;
        ExportPath = null;
        Reading = Task.CompletedTask;
        cancellation = null;

        if (token is not null)
        {
            token.Cancel();
            Stop(reading, previous);
            token.Dispose();
        }

        previous?.Dispose();
    }

    /// <summary>Waits for the worker to stop, interrupting the store until it has.</summary>
    /// <remarks>
    /// Interrupted repeatedly rather than once: an interrupt that arrives before the store
    /// begins a statement is simply lost, and the worker's next stage may be a statement that
    /// runs for minutes. Once cancellation is requested the worker cannot start another table,
    /// so this converges.
    /// </remarks>
    private static void Stop(Task reading, ReaderSession? session)
    {
        try
        {
            while (!reading.Wait(TimeSpan.FromMilliseconds(20)))
            {
                session?.Interrupt();
            }
        }
        catch (AggregateException)
        {
            // A cancelled read faults its task with OperationCanceledException, which is the
            // outcome that was asked for. Anything else the read threw has already been
            // contained per table and reported as a finding.
        }
    }
}
