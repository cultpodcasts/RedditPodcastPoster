using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Languages;

public static class CatalogueLanguageTextReader
{
    public static CatalogueLanguageTexts FromCategorisedItem(
        CategorisedItem item,
        string? nameOverride,
        Publisher? existingParent = null)
    {
        string? showName = null;
        string? showDescription = null;
        string? childTitle = null;
        string? childDescription = null;
        switch (item.Authority)
        {
            case Service.Apple:
                showName = item.ResolvedAppleItem?.ShowName;
                showDescription = item.ResolvedAppleItem?.ShowDescription;
                childTitle = item.ResolvedAppleItem?.EpisodeTitle;
                childDescription = item.ResolvedAppleItem?.EpisodeDescription;
                break;
            case Service.Spotify:
                showName = item.ResolvedSpotifyItem?.ShowName;
                showDescription = item.ResolvedSpotifyItem?.ShowDescription;
                childTitle = item.ResolvedSpotifyItem?.EpisodeTitle;
                childDescription = item.ResolvedSpotifyItem?.EpisodeDescription;
                break;
            case Service.YouTube:
                showName = item.ResolvedYouTubeItem?.ShowName;
                showDescription = item.ResolvedYouTubeItem?.ShowDescription;
                childTitle = item.ResolvedYouTubeItem?.EpisodeTitle;
                childDescription = item.ResolvedYouTubeItem?.EpisodeDescription;
                break;
            case Service.Other:
                showName = item.ResolvedNonPodcastServiceItem?.ShowName;
                childTitle = item.ResolvedNonPodcastServiceItem?.Title;
                childDescription = item.ResolvedNonPodcastServiceItem?.Description;
                break;
        }

        var parentTitle = First(existingParent?.Name, nameOverride, showName);
        var parentDescription = First(existingParent?.Description, showDescription);
        return CatalogueLanguageTexts.From(
            parentTitle,
            parentDescription,
            childTitle,
            childDescription,
            "Podcast",
            "Episode");
    }

    public static CatalogueLanguageTexts ForParentAndChild(
        string? parentTitle,
        string? parentDescription,
        string? childTitle,
        string? childDescription,
        string parentNoun,
        string childNoun) =>
        CatalogueLanguageTexts.From(
            parentTitle,
            parentDescription,
            childTitle,
            childDescription,
            parentNoun,
            childNoun);

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
