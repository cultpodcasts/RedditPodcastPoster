using Microsoft.Extensions.Logging;
using RedditPodcastPoster.BBC;
using RedditPodcastPoster.InternetArchive;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class NonPodcastServiceCategoriser(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
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
                matchingPodcastIds = await episodeRepository
                    .GetAllBy(episode => episode.Urls.BBC == url)
                    .Select(x => x.PodcastId)
                    .ToListAsync();
            }
            else if (InternetArchiveUrlMatcher.IsInternetArchiveUrl(url))
            {
                service = NonPodcastService.InternetArchive;
                matchingPodcastIds = await episodeRepository
                    .GetAllBy(episode => episode.Urls.InternetArchive == url)
                    .Select(x => x.PodcastId)
                    .ToListAsync();
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
                    episodes = await episodeRepository
                        .GetByPodcastId(podcast.Id, x => x.Urls.BBC == url)
                        .ToListAsync();

                }
                else
                {
                    episodes = await episodeRepository
                        .GetByPodcastId(podcast.Id, x => x.Urls.InternetArchive == url)
                        .ToListAsync();
                }

                if (episodes.Count() > 1)
                {
                    throw new InvalidOperationException(
                        $"Found episodes with url '{url}'. Podcast-id: '{podcast.Id}'. Episode-ids: {string.Join(", ", episodes)}.");
                }

                return new ResolvedNonPodcastServiceItem(service, podcast, episodes.Single());
            }
        }
        var podcastEpisodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();

        return await streamingServiceMetaDataHandler.ResolveServiceItem(podcast, podcastEpisodes, url);
    }
}