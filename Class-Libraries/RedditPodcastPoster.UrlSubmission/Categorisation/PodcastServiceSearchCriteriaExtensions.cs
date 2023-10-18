using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public static class PodcastServiceSearchCriteriaExtensions
{
    public static PodcastServiceSearchCriteria Merge(this PodcastServiceSearchCriteria criteria,
        ResolvedSpotifyItem item)
    {
        return new PodcastServiceSearchCriteria(
            !string.IsNullOrWhiteSpace(criteria.ShowName) ? criteria.ShowName : item.ShowName,
            !string.IsNullOrWhiteSpace(criteria.ShowDescription) ? criteria.ShowDescription : item.ShowDescription,
            !string.IsNullOrWhiteSpace(criteria.Publisher) ? criteria.Publisher : item.Publisher,
            !string.IsNullOrWhiteSpace(criteria.EpisodeTitle) ? criteria.EpisodeTitle : item.EpisodeTitle,
            !string.IsNullOrWhiteSpace(criteria.EpisodeDescription)
                ? criteria.EpisodeDescription
                : item.EpisodeDescription,
            criteria.Release > DateTime.MinValue ? criteria.Release : item.Release,
            criteria.Duration > TimeSpan.MinValue ? criteria.Duration : item.Duration
        );
    }

    public static PodcastServiceSearchCriteria Merge(this PodcastServiceSearchCriteria criteria,
        ResolvedYouTubeItem item)
    {
        return new PodcastServiceSearchCriteria(
            !string.IsNullOrWhiteSpace(criteria.ShowName) ? criteria.ShowName : item.ShowName,
            !string.IsNullOrWhiteSpace(criteria.ShowDescription) ? criteria.ShowDescription : item.ShowDescription,
            !string.IsNullOrWhiteSpace(criteria.Publisher) ? criteria.Publisher : item.Publisher,
            !string.IsNullOrWhiteSpace(criteria.EpisodeTitle) ? criteria.EpisodeTitle : item.EpisodeTitle,
            !string.IsNullOrWhiteSpace(criteria.EpisodeDescription)
                ? criteria.EpisodeDescription
                : item.EpisodeDescription,
            criteria.Release > DateTime.MinValue ? criteria.Release : item.Release,
            criteria.Duration > TimeSpan.MinValue ? criteria.Duration : item.Duration
        );
    }

    public static PodcastServiceSearchCriteria Merge(this PodcastServiceSearchCriteria criteria, ResolvedAppleItem item)
    {
        return new PodcastServiceSearchCriteria(
            !string.IsNullOrWhiteSpace(criteria.ShowName) ? criteria.ShowName : item.ShowName,
            !string.IsNullOrWhiteSpace(criteria.ShowDescription) ? criteria.ShowDescription : item.ShowDescription,
            !string.IsNullOrWhiteSpace(criteria.Publisher) ? criteria.Publisher : item.Publisher,
            !string.IsNullOrWhiteSpace(criteria.EpisodeTitle) ? criteria.EpisodeTitle : item.EpisodeTitle,
            !string.IsNullOrWhiteSpace(criteria.EpisodeDescription)
                ? criteria.EpisodeDescription
                : item.EpisodeDescription,
            criteria.Release > DateTime.MinValue ? criteria.Release : item.Release,
            criteria.Duration > TimeSpan.MinValue ? criteria.Duration : item.Duration
        );
    }
}