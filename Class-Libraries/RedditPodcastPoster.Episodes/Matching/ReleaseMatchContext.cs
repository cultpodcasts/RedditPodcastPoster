using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Matching;

public sealed record ReleaseMatchContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);
