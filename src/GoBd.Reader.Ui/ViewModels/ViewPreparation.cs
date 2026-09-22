using GoBd.Reader.Data;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>What preparing a view came to.</summary>
/// <param name="Rows">The records to show, when it was prepared.</param>
/// <param name="Superseded">True when a newer request replaced this one before it finished.</param>
/// <param name="Failure">Why it could not be prepared, when it could not.</param>
public sealed record PreparedView(TableRows? Rows, bool Superseded, string? Failure)
{
    /// <summary>True when there are records to show.</summary>
    public bool IsReady => Rows is not null;
}

/// <summary>
/// Prepares one tab's view without blocking the reader.
/// </summary>
/// <remarks>
/// Filtering four million records takes about a second, and sorting them rather longer. That has
/// to happen off the thread the window draws on, and a person changing their mind halfway must
/// not wait for the answer they no longer want — so a newer request stops the statement the older
/// one is running rather than queueing behind it. See the change's design.md D5.
/// <para>
/// Nothing is shown until it is ready: while a view is being prepared the tab keeps showing what
/// it had, and a preparation that fails leaves that standing too. The alternative — an empty grid
/// while the store works — would read as a table with no records in it.
/// </para>
/// </remarks>
/// <param name="session">The open export.</param>
/// <param name="tab">Which view of a table this prepares, so a tab replaces only its own.</param>
/// <param name="settle">
/// How long to wait before asking the store, for an editor that reports a value as it is typed.
/// Nothing does today — every request comes from applying, removing or resetting something — so
/// the default is none: a quarter of a second added to a button press is a quarter of a second a
/// person waits for no reason.
/// </param>
public sealed class ViewPreparation(ReaderSession session, int tab, TimeSpan? settle = null) : IDisposable
{
    private readonly TimeSpan settle = settle ?? TimeSpan.Zero;

    /// <summary>
    /// Held for the whole of one preparation, so that this tab prepares one view at a time.
    /// </summary>
    /// <remarks>
    /// Two of them at once would be two threads on the tab's one connection, which the store does
    /// not allow, and two of them racing to record what is running would leave the loser's
    /// cancellation unreachable — nothing could then stop it, and nobody would dispose it.
    /// </remarks>
    private readonly SemaphoreSlim gate = new(1, 1);

    private CancellationTokenSource? running;

    /// <summary>Which tab this prepares for, which is the connection the store answers it on.</summary>
    public int Tab => tab;

    /// <summary>Preparations that were replaced before they finished.</summary>
    public int Superseded { get; private set; }

    /// <summary>
    /// Prepares what a tab has asked to see, replacing whatever this tab was preparing.
    /// </summary>
    /// <param name="table">The table being looked at.</param>
    /// <param name="query">Which records, and in what order.</param>
    /// <param name="showing">The view the tab is showing now, which is dropped once it is replaced.</param>
    public async Task<PreparedView> PrepareAsync(TableNode table, TableQuery query, string? showing)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(query);

        // Stopped before the gate is asked for, because what is running holds it: waiting first
        // would be waiting for the very work this request supersedes.
        Stop();

        try
        {
            await gate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The tab was closed while this was on its way in. It prepares nothing, and says so
            // the way a superseded request does.
            return new PreparedView(null, Superseded: true, Failure: null);
        }

        var cancellation = new CancellationTokenSource();
        Volatile.Write(ref running, cancellation);
        var token = cancellation.Token;

        try
        {
            var rows = await Task.Run(
                async () =>
                {
                    if (settle > TimeSpan.Zero)
                    {
                        await Task.Delay(settle, token).ConfigureAwait(false);
                    }

                    return session.Build(table, query, tab, token);
                },
                token).ConfigureAwait(false);

            // The view it replaces goes once this one is ready to be shown, and never before: the
            // grid may still be paging it. A tab that asked for the table itself has no new view,
            // and the one it had goes just the same.
            if (showing is not null && showing != rows.View)
            {
                session.Discard(showing, tab);
            }

            return new PreparedView(rows, Superseded: false, Failure: null);
        }
        catch (OperationCanceledException)
        {
            return new PreparedView(null, Superseded: true, Failure: null);
        }
#pragma warning disable CA1031 // Whatever the store could not do, this tab says so and keeps what it had.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // Including running out of room for the view's own records. The tab keeps the view it
            // has and states this; every other tab is unaffected.
            return new PreparedView(null, Superseded: false, exception.Message.Split('\n')[0].Trim());
        }
        finally
        {
            // This preparation owns its cancellation from here to here: whoever stopped it only
            // cancelled it, so there is exactly one dispose and no window in which a token in use
            // has already been disposed.
            Interlocked.CompareExchange(ref running, null, cancellation);
            cancellation.Dispose();
            gate.Release();
        }
    }

    /// <summary>Stops whatever this tab is preparing, and closes what it was preparing on.</summary>
    /// <remarks>
    /// Waits for the preparation it just stopped, because the store cannot close a connection with
    /// a statement still running on it. The wait is short: the statement has been interrupted, and
    /// the preparation continues on a worker rather than on the thread closing the tab.
    /// </remarks>
    public void Dispose()
    {
        Stop();
        gate.Wait();
        try
        {
            session.CloseQueries(tab);
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    /// <summary>
    /// Stops the preparation in flight, if there is one.
    /// </summary>
    /// <remarks>
    /// Both halves are needed: the token ends the waiting, and the interrupt ends a statement
    /// already inside the store, which a token cannot reach. It is this tab's own querying
    /// connection that is stopped — never the importing one, which would abandon reading the
    /// export, and never another tab's, which is answering someone who is still waiting.
    /// </remarks>
    private void Stop()
    {
        if (Volatile.Read(ref running) is not { } cancellation)
        {
            return;
        }

        Superseded++;
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // It finished between being read and being cancelled, and disposed itself. There is
            // nothing left to stop.
            return;
        }

        session.InterruptQuery(tab);
    }
}
