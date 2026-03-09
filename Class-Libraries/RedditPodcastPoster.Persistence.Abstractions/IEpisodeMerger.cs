using LegacyPodcast = RedditPodcastPoster.Models.Podcast;
using LegacyEpisode = RedditPodcastPoster.Models.Episode;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeMerger
{
    Task<EpisodeMergeResult> MergeEpisodes(
        LegacyPodcast podcast,
        IEnumerable<LegacyEpisode> existingEpisodes,
        IEnumerable<LegacyEpisode> episodesToMerge);
}

public record EpisodeMergeResult(
    IList<V2Episode> EpisodesToSave,
    IList<LegacyEpisode> AddedEpisodes,
    IList<(LegacyEpisode Existing, LegacyEpisode NewDetails)> MergedEpisodes,
    IList<IEnumerable<LegacyEpisode>> FailedEpisodes);
