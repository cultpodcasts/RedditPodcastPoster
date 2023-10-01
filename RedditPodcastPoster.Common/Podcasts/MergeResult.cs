using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public record MergeResult(
    Guid PodcastId, 
    string PodcastName, 
    string PodcastPublisher, 
    List<Episode> AddedEpisodes,
    List<(Episode Existing, Episode NewDetails)> MergedEpisodes, 
    List<IEnumerable<Episode>> failedEpisodes);