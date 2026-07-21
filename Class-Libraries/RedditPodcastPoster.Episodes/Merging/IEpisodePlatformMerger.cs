using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Merging;

public interface IEpisodePlatformMerger
{
    bool MergeInPlace(Episode existingEpisode, Episode incomingEpisode, Podcast podcast);
}
