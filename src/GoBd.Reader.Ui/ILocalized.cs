using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui;

/// <summary>Something that says what it says in a display language, and can be told another.</summary>
internal interface ILocalized
{
    /// <summary>Says everything in the given language from now on.</summary>
    void SetLanguage(ReportLanguage language);
}
