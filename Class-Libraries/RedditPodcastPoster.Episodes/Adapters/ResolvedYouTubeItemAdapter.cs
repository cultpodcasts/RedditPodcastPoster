using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Adapters;

public sealed class ResolvedYouTubeItemAdapter : IEpisodeCatalogueAdapter<ResolvedYouTubeItemInput>
{
    public EpisodeCandidate Adapt(ResolvedYouTubeItemInput input) =>
        new(
            input.EpisodeTitle,
            input.EpisodeDescription,
            input.Duration,
            ReleaseInfoFactory.DateTimeUtcRelease(input.Release),
            PlatformLinkFactory.Create(
                Service.YouTube,
                input.EpisodeId,
                input.Url,
                input.Image));
}
