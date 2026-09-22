using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using GoBd.Reader.Ui.Controls;

namespace GoBd.Reader.Ui;

/// <summary>
/// A window that shows a text to be read, such as the reader's licence or the notices of the
/// components it contains.
/// </summary>
/// <remarks>
/// Not modal: an export may be reading in the background, and a person reading the notices should
/// still be able to use the main window. The text is set in the same monospaced face as the
/// summary's codes, because the notices were written for one, and long lines wrap rather than
/// scroll sideways.
/// <para>
/// A short text is one block, which can be selected across its whole length as a licence should
/// be. A long one — the notices run to thousands of lines — is a list of its lines, which the list
/// virtualises: as one block it laid out every line on every pass, and the window dragged
/// noticeably. Lines are selected rather than characters there, and copying takes the selected
/// lines, or all of them when nothing is selected. See the prepare-public-release change's
/// design.md D5.
/// </para>
/// </remarks>
internal sealed class TextWindow : Window
{
    /// <summary>Lines beyond which a text is shown as a virtualised list rather than one block.</summary>
    /// <remarks>
    /// Well above the licence and what it does not cover, and far below the notices. A text of a
    /// few hundred lines lays out in one pass without being felt.
    /// </remarks>
    internal const int LinesBeyondWhichItIsAList = 400;

    /// <summary>Creates the window with its title and text.</summary>
    public TextWindow(string title, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Title = title;
        Icon = ReaderIcon.ForWindow();
        Width = 820;
        Height = 680;
        Text = text;

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length <= LinesBeyondWhichItIsAList)
        {
            Body = new SelectableTextBlock
            {
                Text = text,
                FontFamily = StartPageView.CodeFont,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 16),
            };

            Scroller = new ScrollViewer
            {
                Content = Body,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            Content = Scroller;
            return;
        }

        Lines = new ListBox
        {
            ItemsSource = lines,
            SelectionMode = SelectionMode.Multiple,
            Padding = new Thickness(16, 12),
            // The line itself rather than a binding to it: a binding by path is reflection, which
            // the trimmed reader does not allow, and a line never changes once shown.
            ItemTemplate = new FuncDataTemplate<string>(
                (line, _) => new TextBlock
                {
                    Text = line,
                    FontFamily = StartPageView.CodeFont,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                },
                supportsRecycling: false),
        };

        // A line of a licence is a line, not an entry to be framed and spaced.
        Lines.Styles.Add(new Style(selector => selector.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 0)),
                new Setter(Layoutable.MinHeightProperty, 0d),
            },
        });

        Content = Lines;
        KeyDown += OnKeyDown;
    }

    /// <summary>The text being shown, however it is shown.</summary>
    internal string Text { get; }

    /// <summary>The one block a short text is shown as, or null when it is shown as a list.</summary>
    internal SelectableTextBlock? Body { get; }

    /// <summary>What scrolls that block, or null when the text is shown as a list.</summary>
    internal ScrollViewer? Scroller { get; }

    /// <summary>The list a long text is shown as, or null when it is shown as one block.</summary>
    internal ListBox? Lines { get; }

    /// <summary>Copies the selected lines, or the whole text when none is selected.</summary>
    internal string Selection()
    {
        if (Lines is null)
        {
            return Text;
        }

        var selected = Lines.SelectedItems?.OfType<string>().ToArray() ?? [];
        return selected.Length == 0 ? Text : string.Join(Environment.NewLine, selected);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs args)
    {
        var copy = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (args.Key != Key.C || args.KeyModifiers != copy || Clipboard is null)
        {
            return;
        }

        args.Handled = true;
        try
        {
            await Clipboard.SetTextAsync(Selection());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing here is worth taking the reader down for: this runs as an event handler, so
            // anything it let escape would be unhandled. A clipboard the platform refuses leaves
            // the text on the screen, which is where it was being read.
        }
    }
}
