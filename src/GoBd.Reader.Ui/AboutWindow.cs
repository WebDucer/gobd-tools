using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GoBd.Validation;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>
/// What the reader is: its icon, its name, the version of this build, and the way to its licence,
/// to what that licence does not cover, and to the notices of what it contains.
/// </summary>
/// <remarks>
/// A person opens this to find out which version they are running, usually in order to say so to
/// someone else, so the version is selectable. Everything else here is what an application is
/// expected to say about itself, and the three texts it may not keep from them are a button away.
/// The copyright line comes from the licence the build carries, so it cannot drift from it. See
/// the prepare-public-release change's design.md D5.
/// </remarks>
internal sealed class AboutWindow : Window, ILocalized
{
    private readonly Button close = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock what = new()
    {
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
    };

    private readonly TextBlock terms = new()
    {
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Classes = { ReaderTheme.MutedClass },
    };

    private readonly HyperlinkButton project = new()
    {
        NavigateUri = new Uri(ProjectUrl),
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private readonly Button licence = new();
    private readonly Button notice = new();
    private readonly Button notices = new();

    /// <summary>Where the reader comes from, for the person who wants to see for themselves.</summary>
    internal const string ProjectUrl = "https://github.com/WebDucer/gobd-tools";

    /// <summary>The version of this build, as the About window says it.</summary>
    internal static string VersionText =>
        "Version " + typeof(AboutWindow).Assembly.GetName().Version?.ToString();

    /// <summary>Creates the window, with the entries that open the three texts.</summary>
    public AboutWindow(Action showLicence, Action showNotice, Action showNotices, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(showLicence);
        ArgumentNullException.ThrowIfNull(showNotice);
        ArgumentNullException.ThrowIfNull(showNotices);

        Icon = ReaderIcon.ForWindow();
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        using var file = AssetLoader.Open(ReaderIcon.Png);
        var picture = new Image
        {
            Source = new Bitmap(file),
            Width = 96,
            Height = 96,
        };

        var name = new TextBlock
        {
            Text = "GoBD Reader",
            FontSize = 26,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var version = new SelectableTextBlock
        {
            Text = VersionText,
            Classes = { ReaderTheme.MutedClass },
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        licence.Click += (_, _) => showLicence();
        notice.Click += (_, _) => showNotice();
        notices.Click += (_, _) => showNotices();

        var texts = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { licence, notice, notices },
        };

        close.Click += (_, _) => Close();

        // Sized by its content, so the window grows with the zoom rather than clipping it.
        Content = ReaderWindows.Scrolling(new Zoomed(new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 12,
            Width = 404,
            Children = { picture, name, version, what, terms, project, texts, close },
        }));
        ReaderWindows.OpenUsable(this);

        // Escape closes it, as a window with nothing to decide should, and Cmd+W on macOS.
        KeyDown += (_, args) =>
        {
            if (ReaderKeys.CloseDialog.Matches(args))
            {
                args.Handled = true;
                Close();
            }
        };

        SetLanguage(language);
    }

    /// <inheritdoc />
    public void SetLanguage(ReportLanguage language)
    {
        Title = UiText.About(language);
        what.Text = UiText.AboutDescription(language);

        // The licence writes its copyright "(c)", which a window can set properly.
        terms.Text = UiText.MitLicence(language) + " · " + LicenceTexts.Copyright.Replace("(c)", "©", StringComparison.Ordinal);
        project.Content = UiText.ProjectPage(language);
        licence.Content = UiText.Licence(language);
        notice.Content = UiText.Notice(language);
        notices.Content = UiText.ThirdPartyNotices(language);
        close.Content = UiText.Close(language);
    }
}
