using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Adapters;

public sealed class SpotifyEpisodeAdapter : IEpisodeCatalogueAdapter<SpotifyCatalogueInput>
{
    public EpisodeCandidate Adapt(SpotifyCatalogueInput input) =>
        new(
            input.Title,
            input.Description,
            input.Duration,
            ReleaseInfoFactory.SpotifyRelease(input.Release),
            PlatformLinkFactory.Create(
                Service.Spotify,
                input.SpotifyId,
                input.SpotifyUrl,
                input.Image));
}
