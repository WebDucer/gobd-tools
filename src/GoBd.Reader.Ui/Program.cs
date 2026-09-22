using Avalonia;

namespace GoBd.Reader.Ui;

/// <summary>Entry point.</summary>
public static class Program
{
    /// <summary>Starts the reader, optionally on the export named on the command line.</summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        App.ExportPath = args.FirstOrDefault(argument => !argument.StartsWith('-'));
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>The application, configured for the platform it is running on.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
