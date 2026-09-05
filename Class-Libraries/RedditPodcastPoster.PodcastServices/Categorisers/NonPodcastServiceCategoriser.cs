using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Handlers;

namespace RedditPodcastPoster.PodcastServices.Categorisers;

public class NonPodcastServiceCategoriser(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IStreamingServiceMetaDataHandler streamingServiceMetaDataHandler,
    INonPodcastServiceAdapterResolver adapterResolver,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<NonPodcastServiceCategoriser> logger
#pragma warning restore CS9113 // Parameter is unread.
) : INonPodcastServiceCategoriser
{
    public async Task<ResolvedNonPodcastServiceItem?> Resolve(
        Podcast? podcast,
        Uri url,
        IndexingContext indexingContext,
        NonPodcastServiceItemMetaData? prefetchedMeta = null,
        bool forceMetaExtract = false)
    {
        if (podcast == null)
        {
            var adapter = adapterResolver.ForSubmit(url)
                          ?? throw new InvalidOperationException("Unrecognised service");

            var storedMatches = episodeRepository.GetAllBy(adapter.StoredUrlEquals(url));
            var matchingPodcastIds = storedMatches is null
                ? []
                : await storedMatches.Select(x => x.PodcastId).ToListAsync();

            if (matchingPodcastIds.Any())
            {
                if (matchingPodcastIds.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Found multiple podcasts with url '{url}'. Podcast-ids: {string.Join(", ", matchingPodcastIds)}.");
                }

                var podcastId = matchingPodcastIds.Single();
                podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
                if (podcast == null)
                {
                    throw new InvalidOperationException(
                        $"Podcast '{podcastId}' not found for url '{url}'.");
                }

                var podcastUrlMatches = episodeRepository
                    .GetByPodcastId(podcast.Id, adapter.StoredUrlEquals(url));
                var episodes = podcastUrlMatches is null
                    ? []
                    : await podcastUrlMatches.ToListAsync();

                if (episodes.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Found episodes with url '{url}'. Podcast-id: '{podcast.Id}'. Episode-ids: {string.Join(", ", episodes)}.");
                }

                // Known URL: skip scrape unless refresh-meta (or prefetched meta) needs fields to apply.
                if (!forceMetaExtract && prefetchedMeta is null)
                {
                    return new ResolvedNonPodcastServiceItem(adapter.Service, podcast, episodes.Single(), Url: url);
                }

                return await streamingServiceMetaDataHandler.ResolveServiceItem(
                    podcast, episodes, url, prefetchedMeta);
            }
        }

        List<Episode> podcastEpisodes;
        if (podcast == null)
        {
            podcastEpisodes = [];
        }
        else
        {
            var seriesEpisodes = episodeRepository.GetByPodcastId(podcast.Id);
            podcastEpisodes = seriesEpisodes is null
                ? []
                : await seriesEpisodes.ToListAsync();
        }

        return await streamingServiceMetaDataHandler.ResolveServiceItem(
            podcast, podcastEpisodes, url, prefetchedMeta);
    }
}
