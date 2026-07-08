using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public static class PodcastServiceSearchCriteriaExtensions
{
    public static PodcastServiceSearchCriteria Merge(
        this PodcastServiceSearchCriteria criteria,
        CategorisedSpotifyItem item)
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

    public static PodcastServiceSearchCriteria Merge(
        this PodcastServiceSearchCriteria criteria,
        CategorisedYouTubeItem item)
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

    public static PodcastServiceSearchCriteria Merge(
        this PodcastServiceSearchCriteria criteria,
        CategorisedAppleItem item)
    {
        return new PodcastServiceSearchCriteria(
            !string.IsNullOrWhiteSpace(criteria.ShowName) ? criteria.ShowName : item.ShowName,
            !string.IsNullOrWhiteSpace(criteria.ShowDescription) ? criteria.ShowDescription : item.ShowDescription,
            !string.IsNullOrWhiteSpace(criteria.Publisher) ? criteria.Publisher : item.Publisher,
            !string.IsNullOrWhiteSpace(criteria.EpisodeTitle) ? criteria.EpisodeTitle : item.EpisodeTitle,
            !string.IsNullOrWhiteSpace(criteria.EpisodeDescription)
                ? criteria.EpisodeDescription
                : item.EpisodeDescription,
            criteria.Release.TimeOfDay > TimeSpan.Zero ? criteria.Release : item.Release,
            criteria.Duration > TimeSpan.MinValue ? criteria.Duration : item.Duration
        );
    }
}
