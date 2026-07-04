using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Adapters;

public sealed class ResolvedAppleItemAdapter : IEpisodeCatalogueAdapter<ResolvedAppleItemInput>
{
    public EpisodeCandidate Adapt(ResolvedAppleItemInput input) =>
        new(
            input.EpisodeTitle,
            input.EpisodeDescription,
            input.Duration,
            ReleaseInfoFactory.DateTimeUtcRelease(input.Release),
            PlatformLinkFactory.Create(
                Service.Apple,
                input.EpisodeId?.ToString(),
                input.Url,
                input.Image));
}
