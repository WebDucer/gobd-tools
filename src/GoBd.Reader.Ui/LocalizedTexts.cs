using Avalonia.Controls;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>
/// Texts set once and kept in whichever display language is chosen later.
/// </summary>
/// <remarks>
/// For what is built once and lives on, such as a native menu — on macOS the platform keeps the
/// menu bar it was first given — or the navigator's tree, whose expanded and selected entries a
/// rebuild would lose. A change of language renames them in place.
/// </remarks>
internal sealed class LocalizedTexts
{
    private readonly List<(Action<string> Set, Func<ReportLanguage, string> Text)> entries = [];
    private ReportLanguage language;

    /// <summary>Starts with texts in the given language.</summary>
    public LocalizedTexts(ReportLanguage language) => this.language = language;

    /// <summary>Sets a text in the current language, and again in whichever is chosen later.</summary>
    public void Follow(Action<string> set, Func<ReportLanguage, string> text)
    {
        set(text(language));
        entries.Add((set, text));
    }

    /// <summary>Heads a menu entry in the current language, and in whichever is chosen later.</summary>
    public NativeMenuItem Follow(NativeMenuItem item, Func<ReportLanguage, string> header)
    {
        Follow(text => item.Header = text, header);
        return item;
    }

    /// <summary>Sets every text in the given language.</summary>
    public void Apply(ReportLanguage newLanguage)
    {
        language = newLanguage;
        foreach (var (set, text) in entries)
        {
            set(text(language));
        }
    }
}
