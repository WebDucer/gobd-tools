using System.Reflection;
using GoBd.Validation.Localisation;

namespace GoBd.Reader.Ui.Tests;

public sealed class UiTextTests
{
    [Fact]
    public void AllUiTextMethodsReturnNonEmptyStringsForBothLanguages()
    {
        var methods = typeof(UiText).GetMethods(BindingFlags.Public | BindingFlags.Static);
        methods.Length.ShouldBeGreaterThan(20);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ReportLanguage))
            {
                var en = (string)method.Invoke(null, [ReportLanguage.English])!;
                var de = (string)method.Invoke(null, [ReportLanguage.German])!;

                en.ShouldNotBeNullOrWhiteSpace($"Method {method.Name} returned empty English text");
                de.ShouldNotBeNullOrWhiteSpace($"Method {method.Name} returned empty German text");
            }
        }
    }

    [Fact]
    public void ACountGroupsItsThousandsTheWayItsLanguageDoes()
    {
        // A German reader takes "1,500" for one and a half.
        UiText.ShowingBanner(ReportLanguage.German, 1500, 4000).ShouldBe("Zeige 1.500 von 4.000 Datensätzen");
        UiText.ShowingBanner(ReportLanguage.English, 1500, 4000).ShouldBe("Showing 1,500 of 4,000 records");
        UiText.Count(ReportLanguage.German, 1234567).ShouldBe("1.234.567");
    }
}
