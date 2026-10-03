using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using GoBd.Reader.Ui.ViewModels;
using GoBd.Validation.Localisation;
using GoBd.Validation.Model;

namespace GoBd.Reader.Ui;

/// <summary>
/// Where a window says what assistive technology should announce, without moving focus.
/// </summary>
/// <remarks>
/// What the reader says about its progress or a navigation is shown where it happened, which is not
/// where a screen reader is listening. A live region is: Avalonia's Windows and macOS back ends
/// announce a change to its name. One per window rather than a live setting on every status line,
/// because the visible progress changes ten times a second and announcing that would drown a
/// screen reader; and one element gives tests one place to read what was said. Kept in the tree at
/// no size, because an element that is not shown is not in the automation tree. See the
/// improve-reader-accessibility change's design.md D15.
/// </remarks>
internal sealed class Announcer : TextBlock
{
    private readonly List<(string Text, bool Assertive)> said = [];

    /// <summary>Creates the announcer, polite until told otherwise.</summary>
    public Announcer()
    {
        Width = 1;
        Height = 1;
        Opacity = 0;
        IsHitTestVisible = false;
        Focusable = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    /// <summary>Everything announced so far, oldest first, for a test to read.</summary>
    internal IReadOnlyList<(string Text, bool Assertive)> Said => said;

    /// <summary>Announces a message, interrupting what is being read when it is assertive.</summary>
    public void Announce(string message, bool assertive = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length == 0)
        {
            return;
        }

        AutomationProperties.SetLiveSetting(this, assertive ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);

        // The same message twice is announced twice: a change of name is what is announced.
        var text = message == Text ? message + "​" : message;
        Text = text;
        AutomationProperties.SetName(this, text);
        said.Add((message, assertive));
    }
}

/// <summary>
/// What is worth announcing about reading an export: every tenth of the way, each table as it
/// finishes, and the outcome once.
/// </summary>
/// <remarks>
/// The display is told of progress every hundred milliseconds; a person listening is told only of
/// what changed enough to matter.
/// </remarks>
internal sealed class ReadingAnnouncements
{
    private readonly HashSet<TableNode> finished = [];
    private int tenths = -1;
    private bool complete;

    /// <summary>Starts again, for another export.</summary>
    public void Reset()
    {
        finished.Clear();
        tenths = -1;
        complete = false;
    }

    /// <summary>What is new enough in this reading to be announced, in a language.</summary>
    public IEnumerable<string> Next(ExportReading reading, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(reading);

        foreach (var table in reading.Tables.Where(table => table.IsFinished && finished.Add(table.Table)))
        {
            yield return UiText.TableFinished(language, table.Table.Identity, table.State switch
            {
                TableReadingState.Ready => UiText.StateRead(language),
                TableReadingState.Defective => UiText.StateDefective(language),
                _ => UiText.StateUnreadable(language),
            });
        }

        if (reading.Complete)
        {
            if (!complete)
            {
                complete = true;
                var report = reading.Report;
                yield return (reading.Verdict == GoBd.Validation.Findings.Verdict.Conformant ? UiText.Conformant(language) : UiText.NonConformant(language))
                    + ". " + UiText.ReadingFinished(language, reading.Tables.Count, report.ErrorCount, report.WarningCount);
            }

            yield break;
        }

        var now = (int)Math.Floor(reading.Fraction * 10);
        if (now > tenths)
        {
            tenths = now;
            if (now > 0)
            {
                yield return UiText.ReadingPercent(language, now * 10);
            }
        }
    }
}
