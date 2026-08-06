using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TextSanitiserUniversalKnownTermsTests
{
    [Fact(DisplayName =
        "Universal known-terms apply for a non-English language even when that language has no known terms.")]
    public async Task SanitiseTitle_WithUniversalKnownTerm_AppliesForNonEnglishLanguage()
    {
        // Arrange
        var mocker = new AutoMocker();
        TitleCasingTestSupport.UseRules(
            mocker,
            TitleCasingTestSupport.CreateEnglishDefault(lowerCaseTerms: [], knownTerms: []),
            new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey)
            {
                LowerCaseTerms = [],
                KnownTerms =
                [
                    new KnownTermEntry
                    {
                        Literal = "BBC",
                        Pattern = @"\bBBC\b",
                        Options = "IgnoreCase, Compiled"
                    }
                ]
            },
            new LanguageTitleCasingRulesDocument("pl")
            {
                LowerCaseTerms = [],
                KnownTerms = []
            });
        var sut = mocker.CreateInstance<TextSanitiser>();

        // Act
        var result = await sut.SanitiseTitle("Interview With Bbc Guest", null, [], [], "pl");

        // Assert
        result.Should().Be("Interview With BBC Guest");
    }

    [Fact(DisplayName =
        "Language known-terms apply after universal when both match the same text.")]
    public async Task SanitiseTitle_WithUniversalAndLanguageKnownTerms_AppliesLanguageAfterUniversal()
    {
        // Arrange
        var mocker = new AutoMocker();
        TitleCasingTestSupport.UseRules(
            mocker,
            TitleCasingTestSupport.CreateEnglishDefault(lowerCaseTerms: [], knownTerms: []),
            new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey)
            {
                LowerCaseTerms = [],
                KnownTerms =
                [
                    new KnownTermEntry
                    {
                        Literal = "ORG",
                        Pattern = @"\bORG\b",
                        Options = "IgnoreCase, Compiled"
                    }
                ]
            },
            new LanguageTitleCasingRulesDocument("fr")
            {
                LowerCaseTerms = [],
                KnownTerms =
                [
                    new KnownTermEntry
                    {
                        Literal = "Org Fr",
                        Pattern = @"\bORG\b",
                        Options = "IgnoreCase, Compiled"
                    }
                ]
            });
        var sut = mocker.CreateInstance<TextSanitiser>();

        // Act
        var result = await sut.SanitiseTitle("Talk About Org Tonight", null, [], [], "fr");

        // Assert
        result.Should().Be("Talk About Org Fr Tonight");
    }

    [Fact(DisplayName = "Missing universal document yields empty universal known-terms.")]
    public void GetUniversalKnownTerms_WhenDocumentMissing_ReturnsEmpty()
    {
        // Arrange
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = TitleCasingTestSupport.CreateEnglishDefault(lowerCaseTerms: [], knownTerms: [])
            });

        // Act
        var result = provider.GetUniversalKnownTerms();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "GetKnownTerms for a language does not merge universal known-terms.")]
    public void GetKnownTerms_DoesNotIncludeUniversalTerms()
    {
        // Arrange
        var universalTerm = new KnownTermEntry
        {
            Literal = "BBC",
            Pattern = @"\bBBC\b",
            Options = "IgnoreCase, Compiled"
        };
        var languageTerm = new KnownTermEntry
        {
            Literal = "Local",
            Pattern = @"\bLocal\b",
            Options = "IgnoreCase, Compiled"
        };
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                [LanguageTitleCasingRulesDocument.UniversalLanguageKey] =
                    new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey)
                    {
                        KnownTerms = [universalTerm]
                    },
                ["en"] = TitleCasingTestSupport.CreateEnglishDefault(
                    lowerCaseTerms: [],
                    knownTerms: [languageTerm])
            });

        // Act
        var languageTerms = provider.GetKnownTerms("en");
        var universalTerms = provider.GetUniversalKnownTerms();

        // Assert
        languageTerms.Should().ContainSingle().Which.Literal.Should().Be("Local");
        universalTerms.Should().ContainSingle().Which.Literal.Should().Be("BBC");
    }
}
