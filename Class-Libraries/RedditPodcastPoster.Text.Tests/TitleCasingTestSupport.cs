using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

internal static class TitleCasingTestSupport
{
    public static void UseDefaultEnglishRules(AutoMocker mocker)
    {
        UseRules(mocker, CreateEnglishDefault());
    }

    public static void UseRules(
        AutoMocker mocker,
        params TitleCasingRulesDocument[] documents)
    {
        var byLanguage = documents.ToDictionary(
            d => TitleCasingRulesDocument.NormaliseLanguage(d.Language),
            d => d,
            StringComparer.OrdinalIgnoreCase);
        var provider = new TitleCasingRulesProvider(byLanguage);
        var instance = mocker.GetMock<IAsyncInstance<ITitleCasingRulesProvider>>();
        instance.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(provider);
        mocker.Use(instance.Object);
    }

    public static EnglishTitleCasingRulesDocument CreateEnglishDefault(
        IReadOnlyList<string>? lowerCaseTerms = null,
        IReadOnlyList<KnownTermEntry>? knownTerms = null) =>
        TitleCasingRulesDocument.CreateEnglishDefault(
            lowerCaseTerms ?? LowerCaseTerms.DefaultEnglishWords,
            knownTerms);
}
