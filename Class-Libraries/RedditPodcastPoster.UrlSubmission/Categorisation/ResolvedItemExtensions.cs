using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public static class ResolvedItemExtensions
{
    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this ResolvedSpotifyItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName.Trim(),
            item.ShowDescription.Trim(),
            item.Publisher.Trim(),
            item.EpisodeTitle.Trim(),
            item.EpisodeDescription.Trim(),
            item.Release,
            item.Duration);
    }

    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this ResolvedAppleItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName.Trim(),
            item.ShowDescription?.Trim() ?? string.Empty,
            item.Publisher,
            item.EpisodeTitle.Trim(),
            item.EpisodeDescription.Trim(),
            item.Release,
            item.Duration);
    }

    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this ResolvedYouTubeItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName.Trim(),
            (item.ShowDescription ?? string.Empty).Trim(),
            (item.Publisher ?? string.Empty).Trim(),
            item.EpisodeTitle.Trim(),
            item.EpisodeDescription.Trim(),
            item.Release,
            item.Duration);
    }
}