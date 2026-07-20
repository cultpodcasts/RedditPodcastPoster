using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Matching;

public interface IEpisodeMerger
{
    EpisodeMergeResult MergeEpisodes(
        Podcast podcast,
        IEnumerable<Episode> existingEpisodes,
        IEnumerable<Episode> episodesToMerge);
}
