namespace RedditPodcastPoster.Common.UrlCategorisation;

public static class ResolvedItemExtensions
{
    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this ResolvedSpotifyItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName,
            item.ShowDescription,
            item.Publisher,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration);
    }

    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this ResolvedAppleItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName,
            item.ShowDescription,
            item.Publisher,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration);
    }

    public static PodcastServiceSearchCriteria ToPodcastServiceSearchCriteria(this ResolvedYouTubeItem item)
    {
        return new PodcastServiceSearchCriteria(
            item.ShowName,
            item.ShowDescription,
            item.Publisher,
            item.EpisodeTitle,
            item.EpisodeDescription,
            item.Release,
            item.Duration);
    }
}