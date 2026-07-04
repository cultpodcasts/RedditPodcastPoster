using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Adapters;

public sealed class ResolvedSpotifyItemAdapter : IEpisodeCatalogueAdapter<ResolvedSpotifyItemInput>
{
    public EpisodeCandidate Adapt(ResolvedSpotifyItemInput input) =>
        new(
            input.EpisodeTitle,
            input.EpisodeDescription,
            input.Duration,
            ReleaseInfoFactory.SpotifyRelease(input.Release),
            PlatformLinkFactory.Create(
                Service.Spotify,
                input.EpisodeId,
                input.Url,
                input.Image));
}
