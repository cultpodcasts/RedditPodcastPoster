using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Applying;

public interface IEpisodePlatformApplier
{
    bool ApplyFillMissing(Episode target, EpisodePlatformPatch patch);

    bool ApplyFillMissingRelease(Episode target, DateTime release);
}
