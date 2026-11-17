using VehicleTracking.Application.Alerts;

namespace VehicleTracking.UnitTests;

public class AlertLanguageTests
{
    [Theory]
    [InlineData("nb", "no")]
    [InlineData("nn", "no")]
    [InlineData("NO", "no")]
    [InlineData("nob", "no")]
    [InlineData("nno", "no")]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("sv", "sv")]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("unspecified", null)]
    [InlineData("und", null)]
    [InlineData(null, null)]
    public void Tags_are_normalized(string? tag, string? expected) =>
        Assert.Equal(expected, AlertLanguage.NormalizeTag(tag));

    [Fact]
    public void A_given_tag_is_kept_and_not_marked_as_detected()
    {
        var text = AlertLanguage.Normalize("Avgangen er forsinket", "en");

        Assert.Equal("en", text.Language);
        Assert.False(text.LanguageDetected);
    }

    [Theory]
    [InlineData("Departure about 21 minutes delayed", "en")]
    [InlineData("Avgangen er ca. 21 minutter forsinket", "no")]
    [InlineData("Sørlandsparken", "no")]
    [InlineData("BUSS 5 STOPPER IKKE", "no")]
    public void A_missing_tag_is_guessed_and_flagged(string text, string expected)
    {
        var result = AlertLanguage.Normalize(text, "unspecified");

        Assert.Equal(expected, result.Language);
        Assert.True(result.LanguageDetected);
    }

    [Fact]
    public void A_neutral_text_stays_unknown()
    {
        var result = AlertLanguage.Normalize("Line 5 / 12", null);

        Assert.Null(result.Language);
        Assert.False(result.LanguageDetected);
    }
}
