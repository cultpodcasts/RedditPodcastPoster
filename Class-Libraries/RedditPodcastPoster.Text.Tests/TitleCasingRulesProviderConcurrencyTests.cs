using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TitleCasingRulesProviderConcurrencyTests
{
    [Fact(DisplayName =
        "Title casing lower-case cache: when GetLowerCaseExpressions is called concurrently for the same language, then no concurrent Dictionary corruption occurs, because homepage sanitise parallelises titles.")]
    public async Task get_lower_case_expressions_is_safe_under_parallel_access()
    {
        // Arrange
        var provider = new TitleCasingRulesProvider(
            new Dictionary<string, LanguageTitleCasingRulesDocument>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = LanguageTitleCasingRulesDocument.CreateEnglishDefault(
                    LowerCaseTerms.DefaultEnglishWords,
                    knownTerms: null),
                ["fil"] = new LanguageTitleCasingRulesDocument
                {
                    Language = "fil",
                    LowerCaseTerms = ["sa", "kay", "ng"],
                    KnownTerms = []
                }
            });

        // Act
        var act = async () => await Task.WhenAll(
            Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
            {
                provider.GetLowerCaseExpressions("en");
                provider.GetLowerCaseExpressions("fil");
                provider.GetLowerCaseExpressions("en");
            })));

        // Assert
        await act.Should().NotThrowAsync();
        provider.GetLowerCaseExpressions("en").Should().NotBeEmpty();
        provider.GetLowerCaseExpressions("fil").Keys.Should().Contain("sa");
    }
}
