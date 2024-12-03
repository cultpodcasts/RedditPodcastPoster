using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class NonPodcastServiceCategoriser(
    IPodcastRepository podcastRepository,
#pragma warning disable CS9113 // Parameter is unread.
    IHttpClientFactory httpClientFactory,
    IStreamingServiceMetaDataHandler streamingServiceMetaDataHandler,
    ILogger<NonPodcastServiceCategoriser> logger
#pragma warning restore CS9113 // Parameter is unread.
) : INonPodcastServiceCategoriser
{
    public async Task<ResolvedNonPodcastServiceItem?> Resolve(Podcast? podcast, Uri url,
        IndexingContext indexingContext)
    {
        if (podcast == null)
        {
            List<Guid> matchingPodcastIds;
            NonPodcastService service;
            if (BBCUrlMatcher.IsBBCUrl(url))
            {
                service = NonPodcastService.BBC;
                var wrappedIds = await podcastRepository
                    .GetAllBy(p => p.Episodes.Any(episode => episode.Urls.BBC == url), x => new {id = x.Id})
                    .ToListAsync();
                matchingPodcastIds = wrappedIds.Select(x => x.id).ToList();
            }
            else if (InternetArchiveUrlMatcher.IsInternetArchiveUrl(url))
            {
                service = NonPodcastService.InternetArchive;
                var wrappedIds = await podcastRepository
                    .GetAllBy(p => p.Episodes.Any(episode => episode.Urls.InternetArchive == url), x => new {id = x.Id})
                    .ToListAsync();
                matchingPodcastIds = wrappedIds.Select(x => x.id).ToList();
            }
            else
            {
                throw new InvalidOperationException("Unrecognised service");
            }

            if (matchingPodcastIds.Any())
            {
                if (matchingPodcastIds.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Found multiple podcasts with url '{url}'. Podcast-ids: {string.Join(", ", matchingPodcastIds)}.");
                }

                var podcastId = matchingPodcastIds.Single();
                podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
                IEnumerable<Episode> episodes;
                if (service == NonPodcastService.BBC)
                {
                    episodes = podcast!.Episodes.Where(x => x.Urls.BBC == url);
                }
                else
                {
                    episodes = podcast!.Episodes.Where(x => x.Urls.InternetArchive == url);
                }

                if (episodes.Count() > 1)
                {
                    throw new InvalidOperationException(
                        $"Found episodes with url '{url}'. Podcast-id: '{podcast.Id}'. Episode-ids: {string.Join(", ", episodes)}.");
                }

                return new ResolvedNonPodcastServiceItem(service, podcast, episodes.Single());
            }
        }

        return await streamingServiceMetaDataHandler.ResolveServiceItem(podcast, url);
    }
}