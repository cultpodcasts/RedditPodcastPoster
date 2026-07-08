using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Matching;

public sealed record ReleaseMatchContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);
