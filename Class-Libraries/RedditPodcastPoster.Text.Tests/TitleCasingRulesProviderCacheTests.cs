using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TitleCasingRulesProviderCacheTests
{
    [Fact(DisplayName =
        "Title casing provider: when constructed with English and universal docs, then English lower-case and known-term replacements are precompiled, because homepage sanitise must not compile regexes per title.")]
    public void constructor_precompiles_english_and_universal_replacements()
    {
        // Arrange
        var languageTerm = new KnownTermEntry
        {
            Literal = "BBC",
            Pattern = @"\bBBC\b",
            Options = "IgnoreCase, Compiled"
        };
        var universalTerm = new KnownTermEntry
        {
            Literal = "ONU",
            Pattern = @"\bONU\b",
            Options = "IgnoreCase, Compiled"
        };

        // Act
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = LanguageTitleCasingRulesDocument.CreateEnglishDefault(
                    LowerCaseTerms.DefaultEnglishWords,
                    knownTerms: [languageTerm]),
                [LanguageTitleCasingRulesDocument.UniversalLanguageKey] =
                    new LanguageTitleCasingRulesDocument(LanguageTitleCasingRulesDocument.UniversalLanguageKey)
                    {
                        LowerCaseTerms = [],
                        KnownTerms = [universalTerm]
                    }
            });

        // Assert
        provider.GetLowerCaseExpressions("en").Should().NotBeEmpty();
        provider.GetKnownTermReplacements("en").Should().ContainSingle()
            .Which.Literal.Should().Be("BBC");
        provider.GetUniversalKnownTermReplacements().Should().ContainSingle()
            .Which.Literal.Should().Be("ONU");
    }

    [Fact(DisplayName =
        "Title casing known-term replacements: when requested twice for the same language, then the same compiled list is reused, because replacements are GetOrAdd-cached.")]
    public void get_known_term_replacements_reuses_compiled_list()
    {
        // Arrange
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["fil"] = new LanguageTitleCasingRulesDocument("fil")
                {
                    LowerCaseTerms = [],
                    KnownTerms =
                    [
                        new KnownTermEntry
                        {
                            Literal = "Quiboloy",
                            Pattern = @"\bQuiboloy\b",
                            Options = "IgnoreCase, Compiled"
                        }
                    ]
                }
            });

        // Act
        var first = provider.GetKnownTermReplacements("fil");
        var second = provider.GetKnownTermReplacements("fil");

        // Assert
        ReferenceEquals(first, second).Should().BeTrue();
        first.Should().ContainSingle().Which.Pattern.IsMatch("quiboloy").Should().BeTrue();
    }

    [Fact(DisplayName =
        "Title casing known-term cache: when GetKnownTermReplacements is called concurrently, then no corruption occurs, because homepage sanitise parallelises titles.")]
    public async Task get_known_term_replacements_is_safe_under_parallel_access()
    {
        // Arrange
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = LanguageTitleCasingRulesDocument.CreateEnglishDefault(
                    LowerCaseTerms.DefaultEnglishWords,
                    knownTerms:
                    [
                        new KnownTermEntry
                        {
                            Literal = "BBC",
                            Pattern = @"\bBBC\b",
                            Options = "IgnoreCase, Compiled"
                        }
                    ]),
                ["fil"] = new LanguageTitleCasingRulesDocument("fil")
                {
                    LowerCaseTerms = ["sa"],
                    KnownTerms =
                    [
                        new KnownTermEntry
                        {
                            Literal = "Quiboloy",
                            Pattern = @"\bQuiboloy\b",
                            Options = "IgnoreCase, Compiled"
                        }
                    ]
                }
            });

        // Act
        var act = async () => await Task.WhenAll(
            Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
            {
                provider.GetKnownTermReplacements("en");
                provider.GetUniversalKnownTermReplacements();
                provider.GetKnownTermReplacements("fil");
            })));

        // Assert
        await act.Should().NotThrowAsync();
        provider.GetKnownTermReplacements("en").Should().NotBeEmpty();
        provider.GetKnownTermReplacements("fil").Should().NotBeEmpty();
    }
}
