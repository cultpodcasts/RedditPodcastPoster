using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IPodcastUpdaterFactory podcastUpdaterFactory,
    ISubjectEnricher subjectEnricher,
    ILogger<IndexProcessor> logger)
{
    public async Task Run(IndexRequest request)
    {
        DateTime? releasedSince = null;
        if (request.ReleasedSince > 0)
        {
            releasedSince = DateTimeHelper.DaysAgo(request.ReleasedSince);
        }

        var indexingContext = new IndexingContext(releasedSince)
        {
            SkipExpensiveYouTubeQueries = request.SkipExpensiveYouTubeQueries,
            SkipPodcastDiscovery = request.SkipPodcastDiscovery,
            SkipExpensiveSpotifyQueries = request.SkipExpensiveSpotifyQueries,
            SkipYouTubeUrlResolving = request.SkipYouTubeUrlResolving,
            SkipSpotifyUrlResolving = request.SkipSpotifyUrlResolving
        };

        IEnumerable<Guid> podcastIds;
        if (request.PodcastId.HasValue)
        {
            podcastIds = new[] {request.PodcastId.Value};
        }
        else
        {
            podcastIds = await podcastRepository.GetAllBy(podcast =>
                podcast.IndexAllEpisodes ||
                (podcast.EpisodeIncludeTitleRegex != null && podcast.EpisodeIncludeTitleRegex != ""), x => x.Id);
        }

        var throttler = new SemaphoreSlim(5);
        var indexTasks = podcastIds.Select(async podcastId =>
        {
            await throttler.WaitAsync();
            try
            {
                await IndexPodcast(indexingContext, podcastId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, $"Failure indexing podcast with podcast-id '{podcastId}'.");
            }
            finally
            {
                throttler.Release();
            }

            return false;
        });
        await Task.WhenAll(indexTasks);
    }

    private async Task IndexPodcast(IndexingContext indexingContext, Guid podcastId)
    {
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast != null)
        {
            try
            {
                var podcastUpdater = podcastUpdaterFactory.Create();
                await podcastUpdater.Update(podcast, indexingContext);
                var episodes = podcast.Episodes.Where(x => x.Release >= indexingContext.ReleasedSince);
                foreach (var episode in episodes)
                {
                    await subjectEnricher.EnrichSubjects(episode, new SubjectEnrichmentOptions(
                        podcast.IgnoredAssociatedSubjects,
                        podcast.DefaultSubject));
                }

                await podcastRepository.Save(podcast);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failure updating podcast with id '{podcast.Id}' and name '{podcast.Name}'.");
            }
        }
    }
}