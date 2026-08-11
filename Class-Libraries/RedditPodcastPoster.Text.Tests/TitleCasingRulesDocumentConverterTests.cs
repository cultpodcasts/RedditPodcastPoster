using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TitleCasingRulesDocumentConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new TitleCasingRulesDocumentConverter(), new JsonStringEnumConverter() }
    };

    [Fact(DisplayName =
        "TitleCasingRules JSON: when language is '*', then deserialize yields UniversalTitleCasingRulesDocument without lower-case or ignored-subjects members.")]
    public void deserialize_universal_by_language_star()
    {
        // Arrange
        var json =
            """{"id":"00000000-0000-0000-0000-000000000001","type":"LanguageTitleCasingRules","language":"*","knownTerms":[],"fileKey":"TitleCasingRules-universal"}""";

        // Act
        var document = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);

        // Assert
        document.Should().BeOfType<UniversalTitleCasingRulesDocument>();
        document!.Language.Should().Be("*");
    }

    [Fact(DisplayName =
        "TitleCasingRules JSON: when language is 'en', then deserialize yields EnglishTitleCasingRulesDocument.")]
    public void deserialize_english_by_language_en()
    {
        // Arrange
        var json =
            """{"id":"00000000-0000-0000-0000-000000000002","type":"LanguageTitleCasingRules","language":"en","lowerCaseTerms":["of"],"knownTerms":[],"fileKey":"TitleCasingRules-en"}""";

        // Act
        var document = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);

        // Assert
        var english = document.Should().BeOfType<EnglishTitleCasingRulesDocument>().Subject;
        english.LowerCaseTerms.Should().Contain("of");
    }

    [Fact(DisplayName =
        "TitleCasingRules JSON: when language is non-English, then deserialize yields NonEnglishTitleCasingRulesDocument with ignoredSubjects.")]
    public void deserialize_non_english_with_ignored_subjects()
    {
        // Arrange
        var json =
            """{"id":"00000000-0000-0000-0000-000000000003","type":"LanguageTitleCasingRules","language":"es","lowerCaseTerms":[],"knownTerms":[],"ignoredSubjects":["Hoy"],"fileKey":"TitleCasingRules-es"}""";

        // Act
        var document = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);

        // Assert
        var nonEnglish = document.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.IgnoredSubjects.Should().Equal("Hoy");
    }

    [Fact(DisplayName =
        "TitleCasingRules JSON: round-trip NonEnglishTitleCasingRulesDocument preserves ignoredSubjects and does not emit them on English serialize.")]
    public void round_trip_non_english_preserves_ignored_subjects()
    {
        // Arrange
        var original = new NonEnglishTitleCasingRulesDocument("es")
        {
            LowerCaseTerms = ["de"],
            IgnoredSubjects = ["Alpha"]
        };

        // Act
        var json = JsonSerializer.Serialize<TitleCasingRulesDocument>(original, Options);
        var roundTrip = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);
        var englishJson = JsonSerializer.Serialize<TitleCasingRulesDocument>(
            new EnglishTitleCasingRulesDocument { LowerCaseTerms = ["of"] },
            Options);

        // Assert
        roundTrip.Should().BeOfType<NonEnglishTitleCasingRulesDocument>()
            .Which.IgnoredSubjects.Should().Equal("Alpha");
        englishJson.Should().NotContain("ignoredSubjects");
    }
}
