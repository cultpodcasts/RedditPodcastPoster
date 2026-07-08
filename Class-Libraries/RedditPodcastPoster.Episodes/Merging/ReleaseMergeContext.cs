using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Merging;

public sealed record ReleaseMergeContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);
