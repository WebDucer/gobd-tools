using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GoBd.Reader.Ui;

/// <summary>What every window beside the reader does when it opens.</summary>
/// <remarks>
/// Focus moves into it, so it can be used from the keyboard at once rather than after a Tab; and a
/// window that sizes itself to its content is kept within the screen, with its content scrolling,
/// so that nothing in it is out of reach at a large zoom. See the improve-reader-accessibility
/// change's design.md D5.
/// </remarks>
internal static class ReaderWindows
{
    /// <summary>
    /// Gives focus back to a picker when its list closes, so the keyboard carries on from it.
    /// </summary>
    /// <remarks>
    /// A picker gives focus to its list while the list is open, and Avalonia gives it back only to
    /// a picker that can be typed in. Without this, focus stayed on a list that had closed: the next
    /// key reached nothing, and on macOS, where an editor is a window of its own, focus left the
    /// editor altogether. Focus the person has put somewhere else in the meantime — a click into a
    /// field — is left where it is.
    /// </remarks>
    /// <param name="area">What holds the pickers, such as an editor or a window.</param>
    public static void KeepFocusOnPickers(Control area)
    {
        ArgumentNullException.ThrowIfNull(area);

        foreach (var picker in area.GetLogicalDescendants().OfType<ComboBox>())
        {
            picker.DropDownClosed += (_, _) => Dispatcher.UIThread.Post(
                () =>
                {
                    var focused = TopLevel.GetTopLevel(picker)?.FocusManager?.GetFocusedElement() as Visual;
                    if (focused is null || focused is ComboBoxItem || TopLevel.GetTopLevel(focused) is null || !area.IsVisualAncestorOf(focused))
                    {
                        picker.Focus(NavigationMethod.Directional);
                    }
                },
                DispatcherPriority.Input);
        }
    }

    /// <summary>Content that scrolls once the screen is too small for it.</summary>
    public static ScrollViewer Scrolling(Control content) => new() { Content = content };

    /// <summary>Makes a window take focus into its first control, and keep within the screen.</summary>
    public static void OpenUsable(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Opened += (_, _) =>
        {
            if (window.Screens.ScreenFromWindow(window) is { } screen)
            {
                var scaling = screen.Scaling > 0 ? screen.Scaling : 1;
                window.MaxHeight = screen.WorkingArea.Height / scaling;
                window.MaxWidth = screen.WorkingArea.Width / scaling;
            }

            // A control to work with where there is one; otherwise what scrolls, so the keys scroll.
            var reachable = window.GetVisualDescendants()
                .OfType<InputElement>()
                .Where(input => input.Focusable && input.IsEffectivelyVisible && input.IsEffectivelyEnabled)
                .ToArray();
            (reachable.FirstOrDefault(input => input is not ScrollViewer) ?? reachable.FirstOrDefault())
                ?.Focus(NavigationMethod.Tab);
        };
    }
}
