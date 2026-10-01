using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Styling;
using GoBd.Reader.Data;
using GoBd.Validation.Localisation;
using Semi.Avalonia;

namespace GoBd.Reader.Ui;

/// <summary>The reader application.</summary>
/// <remarks>
/// The user interface is built in code rather than in markup. Its one substantial control is a
/// grid whose columns come from <c>index.xml</c> and are therefore different for every table, so
/// there is no static shape for markup to describe.
/// </remarks>
public sealed class App : Application
{
    /// <summary>The export to open, taken from the command line.</summary>
    public static string? ExportPath { get; set; }

    private LocalizedTexts? applicationMenuHeaders;

    /// <summary>Updates the localized headers of the application menu on macOS.</summary>
    public void UpdateApplicationMenuLanguage(ReportLanguage language) =>
        applicationMenuHeaders?.Apply(language);

    /// <inheritdoc />
    public override void Initialize()
    {
        // What macOS puts in the menu bar beside the Apple logo, and what a platform shows for
        // the process generally. Avalonia's default is "Avalonia Application", which tells a
        // person nothing about what they are looking at and everything about what it was built
        // with. On macOS the application bundle's Info.plist names the reader as well; the name
        // set here remains for Windows and Linux, which have no bundle, and for a build run from
        // outside one. See the change's design.md D6.
        Name = "GoBD Reader";
        var semi = new SemiTheme();
        Styles.Add(semi);
        Resources[typeof(TableView)] = ReaderTheme.CreateTableViewTheme(
            semi.TryGetResource(typeof(TableView), null, out var semiTableView) ? semiTableView as ControlTheme : null);

        // macOS shows what an application says about itself in the menu named after it, and takes
        // that menu from the application rather than from any window. It has to be set while the
        // application initialises: the platform builds the menu bar once, and where it finds no
        // menu of ours it installs its own — which is how "About Avalonia" appeared there. See the
        // prepare-public-release change's design.md D5.
        //
        // Headed in English until the reader's window has read the preferences and says which
        // language they choose — before anything is on screen.
        if (OperatingSystem.IsMacOS())
        {
            applicationMenuHeaders = new LocalizedTexts(ReportLanguage.English);
            var applicationMenu = new NativeMenu();
            applicationMenu.Items.Add(applicationMenuHeaders.Follow(
                new NativeMenuItem { Command = new ActionCommand(() => Reader?.ShowAbout()) },
                UiText.About));
            applicationMenu.Items.Add(new NativeMenuItemSeparator());
            applicationMenu.Items.Add(applicationMenuHeaders.Follow(
                new NativeMenuItem
                {
                    Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta),
                    Command = new ActionCommand(() => Reader?.ShowSettings()),
                },
                UiText.SettingsMenu));

            NativeMenu.SetMenu(this, applicationMenu);
        }
    }

    /// <summary>
    /// The reader's window, for the application menu, which outlives no window but belongs to none.
    /// </summary>
    internal MainWindow? Reader { get; set; }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Stores left by runs that are no longer alive are removed before this one starts,
            // so nothing accumulates in the temporary location.
            ExportStore.RemoveAbandonedStores();

            // A single file unpacks its native libraries on first start. This reader claims its
            // own copy for as long as it runs, and once its window is open removes the copies
            // other versions left that no running reader claims. See the change's design.md D2.
            var unpacked = UnpackedCopies.Own();
            var claim = unpacked is null ? null : StoreLock.TryTake(unpacked);
            var window = new MainWindow(ExportPath);
            if (unpacked is not null)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => claim?.Dispose();
                window.Opened += (_, _) => Task.Run(() => UnpackedCopies.RemoveOthers(unpacked));
            }

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
