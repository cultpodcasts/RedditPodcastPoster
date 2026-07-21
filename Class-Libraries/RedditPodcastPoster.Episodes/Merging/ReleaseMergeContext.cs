using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Merging;

public sealed record ReleaseMergeContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);
