using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices;

public class PodcastsUpdater(
    IIndexablePodcastIdProvider indexablePodcastIdProvider,
    IPodcastUpdater podcastUpdater,
    IPodcastRepository podcastRepository,
    IFlushable flushableCaches,
    ILogger<PodcastsUpdater> logger)
    : IPodcastsUpdater
{
    public async Task<bool> UpdatePodcasts(Guid[] podcastIds, IndexingContext indexingContext)
    {
        var success = true;
        var batchBypassState = new IndexingContextExtensions.PodcastBatchBypassState(
            indexingContext.SkipYouTubeUrlResolving,
            indexingContext.SkipSpotifyUrlResolving);
        var youtubeEnabled = !indexingContext.SkipYouTubeUrlResolving;
        var youtubeAuthorityInBatch = 0;
        var youtubeAuthorityIndexedWithYouTubePass = 0;
        var youtubeAuthorityBypassed = 0;

        var podcasts = await Task.WhenAll(podcastIds.Select(async podcastId =>
            (Id: podcastId, Podcast: await podcastRepository.GetPodcast(podcastId))));
        var orderedPodcasts = podcasts
            .OrderByDescending(x => x.Podcast?.DependsOnYouTubeForEpisodeDiscovery() == true)
            .ThenBy(x => x.Id)
            .ToArray();

        logger.LogInformation("{nameofUpdatePodcasts} Indexing Starting.", nameof(UpdatePodcasts));
        foreach (var (podcastId, podcast) in orderedPodcasts)
        {
            var dependsOnYouTubeForDiscovery = podcast?.DependsOnYouTubeForEpisodeDiscovery() == true;
            if (dependsOnYouTubeForDiscovery)
            {
                youtubeAuthorityInBatch++;
            }

            var performAutoIndex = podcast != null &&
                                   (podcast.IndexAllEpisodes ||
                                    !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex));
            if (performAutoIndex)
            {
                // performAutoIndex requires podcast != null; capture for catch (flow analysis does not carry in).
                var podcastToUpdate = podcast!;
                try
                {
                    var podcastIndexingContext = indexingContext.ForPodcastUpdate();
                    var result = await podcastUpdater.Update(podcastToUpdate, false, podcastIndexingContext);
                    batchBypassState = indexingContext.AbsorbPodcastPass(
                        podcastIndexingContext,
                        batchBypassState);
                    var resultReport = result.ToString();
                    if (!result.Success)
                    {
                        logger.LogError("{report}",resultReport);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(resultReport))
                        {
                            logger.LogInformation("{result}",result.ToString());
                        }
                    }

                    if (dependsOnYouTubeForDiscovery)
                    {
                        var youtubeEnriched = result.EnrichmentResult.UpdatedEpisodes
                            .Count(e => e.EnrichmentContext.YouTubeUrlUpdated ||
                                        e.EnrichmentContext.YouTubeIdUpdated);

                        logger.LogWarning(
                            "YouTubeAuthorityPodcastAudit podcast-id='{PodcastId}' podcast-name='{PodcastName}' youtube-enabled='{YouTubeEnabled}' youtube-bypassed='{YouTubeBypassed}' episodes-added='{EpisodesAdded}' youtube-enriched='{YouTubeEnriched}'",
                            podcastToUpdate.Id, podcastToUpdate.Name, youtubeEnabled, result.YouTubeBypassed,
                            result.MergeResult.AddedEpisodes.Count, youtubeEnriched);

                        if (youtubeEnabled)
                        {
                            youtubeAuthorityIndexedWithYouTubePass++;
                            if (result.YouTubeBypassed)
                            {
                                youtubeAuthorityBypassed++;
                            }
                        }
                    }

                    success &= result.Success;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failure updating podcast with id '{podcastId}' and name '{podcastName}'.",
                        podcastToUpdate.Id, podcastToUpdate.Name);
                    success = false;
                }
                finally
                {
                    flushableCaches.Flush();
                }
            }
            else if (dependsOnYouTubeForDiscovery)
            {
                logger.LogWarning(
                    "YouTubeAuthorityPodcastAudit podcast-id='{PodcastId}' podcast-name='{PodcastName}' youtube-enabled='{YouTubeEnabled}' indexed='False'",
                    podcast!.Id, podcast.Name, youtubeEnabled);
            }
        }

        if (youtubeAuthorityInBatch > 0)
        {
            logger.LogWarning(
                "YouTubeAuthorityIndexingAudit youtube-enabled='{YouTubeEnabled}' in-batch='{InBatch}' indexed-with-youtube-pass='{IndexedWithYouTubePass}' youtube-bypassed='{YouTubeBypassed}'",
                youtubeEnabled, youtubeAuthorityInBatch, youtubeAuthorityIndexedWithYouTubePass,
                youtubeAuthorityBypassed);
        }

        indexingContext.ApplyBatchBypassRollup(batchBypassState);

        logger.LogInformation("{nameofUpdatePodcasts} Indexing complete.", nameof(UpdatePodcasts));
        return success;
    }

    public async Task<bool> UpdatePodcasts(IndexingContext indexingContext)
    {
        var podcastIds = indexablePodcastIdProvider.GetIndexablePodcastIds();
        return await UpdatePodcasts(await podcastIds.ToArrayAsync(), indexingContext);
    }
}
