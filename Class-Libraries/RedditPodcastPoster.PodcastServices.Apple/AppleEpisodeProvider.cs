using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeProvider(
    ICachedApplePodcastService applePodcastService,
    ILogger<AppleEpisodeProvider> logger)
    : IAppleEpisodeProvider
{
    public async Task<IList<Episode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var episodes = await applePodcastService.GetEpisodes(podcastId, indexingContext);

        if (episodes == null)
        {
            return null;
        }

        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince.Value).ToList();
        }

        return episodes.Select(x =>
            Episode.FromApple(
                x.Id,
                x.Title.Trim(),
                x.Description.Trim(),
                x.Duration,
                x.Explicit,
                x.Release,
                x.Url)
        ).ToList();
    }
}