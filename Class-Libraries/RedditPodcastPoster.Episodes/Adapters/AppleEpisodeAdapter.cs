using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Adapters;

public sealed class AppleEpisodeAdapter : IEpisodeCatalogueAdapter<AppleCatalogueInput>
{
    public EpisodeCandidate Adapt(AppleCatalogueInput input) =>
        new(
            input.Title,
            input.Description,
            input.Duration,
            ReleaseInfoFactory.DateTimeUtcRelease(input.Release),
            PlatformLinkFactory.Create(
                Service.Apple,
                input.AppleId.ToString(),
                input.AppleUrl,
                input.Image));
}
