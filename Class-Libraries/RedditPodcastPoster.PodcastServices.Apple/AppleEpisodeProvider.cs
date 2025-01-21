using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeProvider(
    ICachedApplePodcastService applePodcastService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<AppleEpisodeProvider> logger)
#pragma warning restore CS9113 // Parameter is unread.
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
                x.Url.CleanAppleUrl(),
                x.Image)
        ).ToList();
    }
}