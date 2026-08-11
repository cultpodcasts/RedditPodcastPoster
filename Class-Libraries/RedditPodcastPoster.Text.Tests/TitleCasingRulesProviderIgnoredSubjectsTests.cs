using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TitleCasingRulesProviderIgnoredSubjectsTests
{
    [Fact(DisplayName =
        "Title-casing cache GetIgnoredSubjects: when English is requested, then empty is returned without loading another language document.")]
    public async Task get_ignored_subjects_english_short_circuits()
    {
        // Arrange
        var loads = 0;
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, TitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = new EnglishTitleCasingRulesDocument()
            },
            loadLanguage: (_, _) =>
            {
                loads++;
                return Task.FromResult<TitleCasingRulesDocument?>(null);
            });

        // Act
        var ignored = await provider.GetIgnoredSubjectsAsync("en");

        // Assert
        ignored.Should().BeEmpty();
        loads.Should().Be(0);
    }

    [Fact(DisplayName =
        "Title-casing cache: when lower-case then ignored-subjects are read for the same non-English language, then the repository load runs once.")]
    public async Task casing_then_ignored_subjects_loads_language_once()
    {
        // Arrange
        var loads = 0;
        var spanish = new NonEnglishTitleCasingRulesDocument("es")
        {
            LowerCaseTerms = ["de"],
            IgnoredSubjects = ["Hoy"]
        };
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, TitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = new EnglishTitleCasingRulesDocument()
            },
            loadLanguage: (language, _) =>
            {
                loads++;
                language.Should().Be("es");
                return Task.FromResult<TitleCasingRulesDocument?>(spanish);
            });

        // Act
        await provider.EnsureLanguageLoadedAsync("es");
        _ = provider.GetLowerCaseExpressions("es");
        var ignored = await provider.GetIgnoredSubjectsAsync("es");

        // Assert
        loads.Should().Be(1);
        ignored.Should().Equal("Hoy");
        provider.GetLowerCaseExpressions("es").Keys.Should().Contain("de");
    }
}
