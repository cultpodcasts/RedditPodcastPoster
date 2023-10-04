using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public record MergeResult(
    List<Episode> AddedEpisodes,
    List<(Episode Existing, Episode NewDetails)> MergedEpisodes, 
    List<IEnumerable<Episode>> FailedEpisodes);