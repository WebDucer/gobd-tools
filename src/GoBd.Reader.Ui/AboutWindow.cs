using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GoBd.Validation;

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
internal sealed class AboutWindow : Window
{
    /// <summary>Where the reader comes from, for the person who wants to see for themselves.</summary>
    internal const string ProjectUrl = "https://github.com/WebDucer/gobd-tools";

    /// <summary>The version of this build, as the About window says it.</summary>
    internal static string VersionText =>
        "Version " + typeof(AboutWindow).Assembly.GetName().Version?.ToString();

    /// <summary>Creates the window, with the entries that open the three texts.</summary>
    public AboutWindow(Action showLicence, Action showNotice, Action showNotices)
    {
        ArgumentNullException.ThrowIfNull(showLicence);
        ArgumentNullException.ThrowIfNull(showNotice);
        ArgumentNullException.ThrowIfNull(showNotices);

        Title = MainWindow.AboutTitle;
        Icon = ReaderIcon.ForWindow();
        Width = 460;
        SizeToContent = SizeToContent.Height;
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
            Opacity = 0.75,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var what = new TextBlock
        {
            Text = "Reads GoBD/GDPdU data carrier exports, as described by Beschreibungsstandard 1.6.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.85,
        };

        var terms = new TextBlock
        {
            // The licence writes its copyright "(c)", which a window can set properly.
            Text = "MIT licence · " + LicenceTexts.Copyright.Replace("(c)", "©", StringComparison.Ordinal),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.7,
        };

        var project = new HyperlinkButton
        {
            Content = "Project page",
            NavigateUri = new Uri(ProjectUrl),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var licence = new Button { Content = MainWindow.LicenceTitle };
        licence.Click += (_, _) => showLicence();

        var notice = new Button { Content = MainWindow.NoticeTitle };
        notice.Click += (_, _) => showNotice();

        var notices = new Button { Content = MainWindow.NoticesTitle };
        notices.Click += (_, _) => showNotices();

        var texts = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { licence, notice, notices },
        };

        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(28, 24),
            Spacing = 12,
            Children = { picture, name, version, what, terms, project, texts, close },
        };

        // Escape closes it, as a window with nothing to decide should.
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                Close();
            }
        };
    }
}
