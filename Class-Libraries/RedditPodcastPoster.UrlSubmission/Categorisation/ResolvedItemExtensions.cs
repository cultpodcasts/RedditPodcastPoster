using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public static class ResolvedItemExtensions
{
    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this CategorisedSpotifyItem item)
    {
        var showName = item.ShowName == null || string.IsNullOrWhiteSpace(item.ShowName)
            ? string.Empty
            : item.ShowName.Trim();
        var showDescription = item.ShowDescription == null || string.IsNullOrWhiteSpace(item.ShowDescription)
            ? string.Empty
            : item.ShowDescription.Trim();
        var publisher = item.Publisher == null || string.IsNullOrWhiteSpace(item.Publisher)
            ? string.Empty
            : item.Publisher.Trim();

        return new PodcastServiceSearchCriteria(
            showName,
            showDescription,
            publisher,
            item.EpisodeTitle.Trim(),
            item.EpisodeDescription.Trim(),
            item.Release,
            item.Duration)
        {
            SourceAuthority = Service.Spotify
        };
    }

    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this CategorisedAppleItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName.Trim(),
            item.ShowDescription?.Trim() ?? string.Empty,
            item.Publisher,
            item.EpisodeTitle.Trim(),
            item.EpisodeDescription.Trim(),
            item.Release,
            item.Duration)
        {
            SourceAuthority = Service.Apple
        };
    }

    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this CategorisedYouTubeItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName.Trim(),
            (item.ShowDescription ?? string.Empty).Trim(),
            (item.Publisher ?? string.Empty).Trim(),
            item.EpisodeTitle.Trim(),
            item.EpisodeDescription.Trim(),
            item.Release,
            item.Duration)
        {
            SourceAuthority = Service.YouTube
        };
    }
}
