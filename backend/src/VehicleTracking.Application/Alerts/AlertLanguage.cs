using System.Text.RegularExpressions;

namespace VehicleTracking.Application.Alerts;

/// <summary>
/// Entur publishes most alerts with two texts and no language tag (one English, one Norwegian),
/// so a client cannot pick the right one. This normalizes the tags that exist and guesses the rest
/// from a handful of very common words; a guess is flagged so it is never mistaken for data.
/// </summary>
public static partial class AlertLanguage
{
    private static readonly HashSet<string> NorwegianWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "er", "og", "ikke", "til", "fra", "på", "med", "avgang", "avgangen", "innstilt", "forsinket",
        "stopper", "holdeplass", "buss", "tog", "grunnet", "ca", "minutter",
        // Common short headers seen in the live feed (see the README observations).
        "innstilling", "kansellert", "trafikkmelding", "bestilling", "mottatt", "ingen", "redusert", "kapasitet",
        "forsinkelse", "forventes", "stenger", "flyttes", "reisen", "banen", "linje", "vogner", "erstatter",
    };

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "is", "to", "from", "cancelled", "delayed", "departure", "will", "not", "stop", "due", "minutes",
        "fewer", "carriages", "bus", "train", "trains", "use", "other", "expected", "delay", "delays",
        "planned", "on", "and", "replacement", "closed",
    };

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex Words();

    /// <summary>Returns the text with a normalized tag, guessing one when the feed gave none.</summary>
    public static AlertText Normalize(string text, string? tag)
    {
        if (NormalizeTag(tag) is { } given) return new AlertText(text, given, LanguageDetected: false);

        var guessed = Detect(text);
        return new AlertText(text, guessed, LanguageDetected: guessed is not null);
    }

    /// <summary>Null for "no usable tag". Unknown languages are kept as their lower-case primary subtag.</summary>
    public static string? NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var primary = tag.Trim().ToLowerInvariant().Split('-', '_')[0];
        return primary switch
        {
            "" or "unspecified" or "und" => null,
            "nb" or "nn" or "no" or "nob" or "nno" => "no",
            "en" or "eng" => "en",
            _ => primary,
        };
    }

    public static string? Detect(string text)
    {
        if (text.AsSpan().IndexOfAny("æøåÆØÅ") >= 0) return "no";

        var words = Words().Matches(text).Select(m => m.Value).ToList();
        if (words.Any(NorwegianWords.Contains)) return "no";
        if (words.Any(EnglishWords.Contains)) return "en";
        return null;
    }
}
