using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Handlers;

public class StreamingServiceMetaDataHandler(
    INonPodcastServiceAdapterResolver adapterResolver,
    ILogger<StreamingServiceMetaDataHandler> logger
) : IStreamingServiceMetaDataHandler
{
    public async Task<ResolvedNonPodcastServiceItem> ResolveServiceItem(
        Podcast? podcast,
        IEnumerable<Episode> episodes,
        Uri url)
    {
        var adapter = adapterResolver.ForExtract(url)
                      ?? throw new InvalidOperationException($"Url $'{url}' cannot be handled");

        var metaData = await adapter.ExtractMetaData(url);
        var matchingEpisode = adapter.FindMatchingEpisode(episodes, url);
        if (episodes.Count(episode => adapter.FindMatchingEpisode([episode], url) != null) > 1)
        {
            logger.LogError(
                "Multiple episodes of podcast with podcast-id {podcastId} with url '{url}'.",
                podcast?.Id, url);
        }

        return new ResolvedNonPodcastServiceItem(
            adapter.Service,
            podcast,
            matchingEpisode,
            url,
            metaData.Title,
            metaData.Description,
            metaData.Publisher,
            metaData.Image,
            metaData.Release,
            metaData.Duration,
            metaData.Explicit,
            metaData.ShowName);
    }
}
