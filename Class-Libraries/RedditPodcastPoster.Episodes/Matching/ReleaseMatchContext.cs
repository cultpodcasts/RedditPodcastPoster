using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Matching;

public sealed record ReleaseMatchContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);
