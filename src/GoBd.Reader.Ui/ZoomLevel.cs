using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace GoBd.Reader.Ui;

/// <summary>
/// How large the reader draws its interface, from actual size to twice that, shared by every
/// window it shows.
/// </summary>
/// <remarks>
/// A scale rather than a larger font. Semi sizes its controls' text through aliases a later
/// override does not reach, and the reader's own widths would clip enlarged text; a scale enlarges
/// text, controls, spacing and icons together, the way a browser zooms a page. See the
/// improve-reader-accessibility change's design.md D5.
/// </remarks>
internal sealed class ZoomLevel
{
    /// <summary>The steps zooming in and out moves between, in percent.</summary>
    public static IReadOnlyList<int> Steps { get; } = [100, 110, 125, 150, 175, 200];

    /// <summary>The zoom level of the reader's windows.</summary>
    public static ZoomLevel Current { get; } = new();

    /// <summary>The zoom level, in percent of the actual size.</summary>
    public int Percent { get; private set; } = UserPreferences.ActualSize;

    /// <summary>The factor the interface is drawn at.</summary>
    public double Scale => Percent / 100d;

    /// <summary>Raised when the zoom level changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Sets the zoom level, kept within the range the reader offers.</summary>
    public void Set(int percent)
    {
        var next = Math.Clamp(percent, Steps[0], Steps[^1]);
        if (next == Percent)
        {
            return;
        }

        Percent = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The next step larger than the current one, or the largest.</summary>
    public int Larger() => Steps.FirstOrDefault(step => step > Percent, Steps[^1]);

    /// <summary>The next step smaller than the current one, or the smallest.</summary>
    public int Smaller() => Steps.LastOrDefault(step => step < Percent, Steps[0]);
}

/// <summary>
/// Content drawn at the reader's zoom level, following it as it changes.
/// </summary>
/// <remarks>
/// Listens only while it is shown, so a closed window or popup is not kept alive by the zoom level
/// it once followed. At actual size it applies no transform at all, so the grid scrolls exactly as
/// it did before there was a zoom.
/// </remarks>
internal sealed class Zoomed : LayoutTransformControl
{
    /// <summary>Wraps content in the reader's zoom.</summary>
    public Zoomed(Control content)
    {
        Child = content;
        Apply();
    }

    /// <summary>The factor this content is drawn at, for a test to read.</summary>
    internal double Scale => (LayoutTransform as ScaleTransform)?.ScaleX ?? 1;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ZoomLevel.Current.Changed += OnZoomChanged;
        Apply();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ZoomLevel.Current.Changed -= OnZoomChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnZoomChanged(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        var scale = ZoomLevel.Current.Scale;
        LayoutTransform = scale == 1 ? null : new ScaleTransform(scale, scale);
    }
}
