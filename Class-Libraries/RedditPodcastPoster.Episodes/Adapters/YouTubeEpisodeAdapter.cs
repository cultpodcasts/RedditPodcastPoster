using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Adapters;

public sealed class YouTubeEpisodeAdapter : IEpisodeCatalogueAdapter<YouTubeCatalogueInput>
{
    public EpisodeCandidate Adapt(YouTubeCatalogueInput input) =>
        new(
            input.Title,
            input.Description,
            input.Duration,
            ReleaseInfoFactory.DateTimeUtcRelease(input.Release),
            PlatformLinkFactory.Create(
                Service.YouTube,
                input.YouTubeId,
                input.YouTubeUrl,
                input.Image));
}
