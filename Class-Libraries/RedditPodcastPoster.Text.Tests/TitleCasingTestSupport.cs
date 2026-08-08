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
        params LanguageTitleCasingRulesDocument[] documents)
    {
        var byLanguage = documents.ToDictionary(
            d => LanguageTitleCasingRulesDocument.NormaliseLanguage(d.Language),
            d => d,
            StringComparer.OrdinalIgnoreCase);
        var provider = new TitleCasingRulesProvider(byLanguage);
        var instance = mocker.GetMock<IAsyncInstance<ITitleCasingRulesProvider>>();
        instance.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(provider);
        mocker.Use(instance.Object);
    }

    public static LanguageTitleCasingRulesDocument CreateEnglishDefault(
        IReadOnlyList<string>? lowerCaseTerms = null,
        IReadOnlyList<KnownTermEntry>? knownTerms = null) =>
        LanguageTitleCasingRulesDocument.CreateEnglishDefault(
            lowerCaseTerms ?? LowerCaseTerms.DefaultEnglishWords,
            knownTerms);
}
