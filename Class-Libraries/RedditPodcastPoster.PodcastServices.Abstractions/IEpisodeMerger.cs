using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IEpisodeMerger
{
    EpisodeMergeResult MergeEpisodes(
        Podcast podcast,
        IEnumerable<Episode> existingEpisodes,
        IEnumerable<Episode> episodesToMerge);
}
