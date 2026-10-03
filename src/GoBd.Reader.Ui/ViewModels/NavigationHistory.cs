using GoBd.Validation.Model;

namespace GoBd.Reader.Ui.ViewModels;

/// <summary>A table, and the record in it a navigation started from or reached.</summary>
/// <param name="Table">The table, as the session knows it.</param>
/// <param name="Ordinal">The record's number as the file counts records, or none for the table alone.</param>
/// <remarks>
/// By record number rather than by position, because a position changes with every filter and
/// sort, and the number the file gives a record does not.
/// </remarks>
public sealed record Place(TableNode Table, long? Ordinal);

/// <summary>
/// Where navigations started and what they reached, so a person can go back and forward again, as
/// a browser does.
/// </summary>
/// <remarks>
/// Only navigations are remembered — following a reference, asking for the records that refer to
/// one, going to a record by number. Choosing a table in the navigator or switching tabs is moving
/// around, not navigating, and stepping between referring records is one walk, so Back from a walk
/// returns to the record it started from. See the improve-reader-accessibility change's design.md
/// D11.
/// </remarks>
public sealed class NavigationHistory
{
    private readonly List<Place> places = [];
    private int at = -1;

    /// <summary>Whether there is an earlier place to go back to.</summary>
    public bool CanGoBack => at > 0;

    /// <summary>Whether there is a later place to go forward to again.</summary>
    public bool CanGoForward => at >= 0 && at < places.Count - 1;

    /// <summary>The place the history stands at, or none before the first navigation.</summary>
    public Place? Current => at >= 0 ? places[at] : null;

    /// <summary>Every place remembered, oldest first, for a test to read.</summary>
    public IReadOnlyList<Place> Places => places;

    /// <summary>
    /// Remembers a navigation from one place to another, discarding the places Forward would have
    /// gone to.
    /// </summary>
    /// <remarks>
    /// The place it started from is remembered only when it is not already where the history
    /// stands, so following one reference after another remembers each place once.
    /// </remarks>
    public void Record(Place from, Place to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        places.RemoveRange(at + 1, places.Count - at - 1);
        if (Current != from)
        {
            places.Add(from);
        }

        places.Add(to);
        at = places.Count - 1;
    }

    /// <summary>Goes back one place, and says which; none when there is nothing to go back to.</summary>
    public Place? Back()
    {
        if (!CanGoBack)
        {
            return null;
        }

        at--;
        return places[at];
    }

    /// <summary>Goes forward one place again, and says which; none when there is nothing to go forward to.</summary>
    public Place? Forward()
    {
        if (!CanGoForward)
        {
            return null;
        }

        at++;
        return places[at];
    }
}
