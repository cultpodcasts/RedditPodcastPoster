using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TitleCasingRulesDocumentConverterTests
{
    /// <summary>
    /// Matches production <c>JsonSerializerOptionsProvider</c> shape for TitleCasingRules:
    /// camelCase, WhenWritingNull, enum strings — converter comes from the type attribute, not Converters.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact(DisplayName =
        "TitleCasingRules JSON: when language is '*', then deserialize yields UniversalTitleCasingRulesDocument without lower-case or ignored-subjects members.")]
    public void deserialize_universal_by_language_star()
    {
        // Arrange
        var id = TitleCasingRulesDocument.IdForLanguage("*");
        var json =
            $$"""{"id":"{{id}}","type":"LanguageTitleCasingRules","language":"*","knownTerms":[{"literal":"Uni","pattern":"Uni"}],"fileKey":"TitleCasingRules-universal"}""";

        // Act
        var document = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);

        // Assert
        var universal = document.Should().BeOfType<UniversalTitleCasingRulesDocument>().Subject;
        universal.Language.Should().Be("*");
        universal.KnownTerms.Should().ContainSingle(t => t.Literal == "Uni" && t.Pattern == "Uni");
    }

    [Fact(DisplayName =
        "TitleCasingRules JSON: when language is 'en', then deserialize yields EnglishTitleCasingRulesDocument with lowerCaseTerms and knownTerms.")]
    public void deserialize_english_by_language_en()
    {
        // Arrange
        var id = TitleCasingRulesDocument.IdForLanguage("en");
        var json =
            $$"""{"id":"{{id}}","type":"LanguageTitleCasingRules","language":"en","lowerCaseTerms":["of"],"knownTerms":[{"literal":"En","pattern":"En"}],"fileKey":"TitleCasingRules-en"}""";

        // Act
        var document = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);

        // Assert
        var english = document.Should().BeOfType<EnglishTitleCasingRulesDocument>().Subject;
        english.LowerCaseTerms.Should().Contain("of");
        english.KnownTerms.Should().ContainSingle(t => t.Literal == "En");
    }

    [Fact(DisplayName =
        "TitleCasingRules JSON: when language is non-English, then deserialize yields NonEnglishTitleCasingRulesDocument with ignoredSubjects, lowerCaseTerms, and knownTerms.")]
    public void deserialize_non_english_with_ignored_subjects()
    {
        // Arrange
        var id = TitleCasingRulesDocument.IdForLanguage("es");
        var json =
            $$"""{"id":"{{id}}","type":"LanguageTitleCasingRules","language":"es","lowerCaseTerms":["de"],"knownTerms":[{"literal":"Es","pattern":"Es"}],"ignoredSubjects":["Hoy"],"fileKey":"TitleCasingRules-es"}""";

        // Act
        var document = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);

        // Assert
        var nonEnglish = document.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.IgnoredSubjects.Should().Equal("Hoy");
        nonEnglish.LowerCaseTerms.Should().Equal("de");
        nonEnglish.KnownTerms.Should().ContainSingle(t => t.Literal == "Es");
    }

    [Fact(DisplayName =
        "TitleCasingRules JSON: round-trip NonEnglishTitleCasingRulesDocument preserves ignoredSubjects, knownTerms, and lowerCaseTerms and does not emit ignoredSubjects on English serialize.")]
    public void round_trip_non_english_preserves_ignored_subjects()
    {
        // Arrange
        var original = new NonEnglishTitleCasingRulesDocument("es")
        {
            LowerCaseTerms = ["de"],
            KnownTerms = [new KnownTermEntry { Literal = "EsTerm", Pattern = @"\bEsTerm\b" }],
            IgnoredSubjects = ["Alpha"]
        };

        // Act
        var json = JsonSerializer.Serialize<TitleCasingRulesDocument>(original, Options);
        var roundTrip = JsonSerializer.Deserialize<TitleCasingRulesDocument>(json, Options);
        var englishJson = JsonSerializer.Serialize<TitleCasingRulesDocument>(
            new EnglishTitleCasingRulesDocument
            {
                LowerCaseTerms = ["of"],
                KnownTerms = [new KnownTermEntry { Literal = "EnTerm", Pattern = @"\bEnTerm\b" }]
            },
            Options);

        // Assert
        var nonEnglish = roundTrip.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.IgnoredSubjects.Should().Equal("Alpha");
        nonEnglish.LowerCaseTerms.Should().Equal("de");
        nonEnglish.KnownTerms.Should().ContainSingle(t => t.Literal == "EsTerm" && t.Pattern == @"\bEsTerm\b");
        englishJson.Should().NotContain("ignoredSubjects");
        englishJson.Should().Contain("knownTerms");
        englishJson.Should().Contain("lowerCaseTerms");
    }
}
