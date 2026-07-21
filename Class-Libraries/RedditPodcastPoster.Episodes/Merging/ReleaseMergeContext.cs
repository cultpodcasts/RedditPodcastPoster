using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Merging;

public sealed record ReleaseMergeContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);
