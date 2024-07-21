using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Search;
using RedditPodcastPoster.Subjects;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IPodcastUpdater podcastUpdater,
    ISubjectEnricher subjectEnricher,
    ISearchIndexerService searchIndexerService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<IndexProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run(IndexRequest request)
    {
        DateTime? releasedSince = null;
        if (request.ReleasedSince > 0)
        {
            releasedSince = DateTimeExtensions.DaysAgo(request.ReleasedSince);
        }

        var indexingContext = new IndexingContext(releasedSince)
        {
            IndexSpotify = !request.SkipSpotifyIndexing,
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
        else if (request.PodcastName != null)
        {
            podcastIds = await podcastRepository.GetAllBy(x =>
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                x => x.Id).ToListAsync();
            logger.LogInformation($"Found {podcastIds.Count()} podcasts.");
        }
        else
        {
            podcastIds = await podcastRepository.GetAllIds().ToArrayAsync();
        }

        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null && !podcast.IsRemoved() &&
                (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))

            {
                logger.LogInformation($"Indexing podcast {podcast.Name}' with podcast-id '{podcastId}'.");
                var results = await podcastUpdater.Update(podcast, indexingContext);
                var resultsMessage = results.ToString();
                if (results.MergeResult.FailedEpisodes.Any() ||
                    (results.SpotifyBypassed && !request.SkipSpotifyUrlResolving) ||
                    (results.YouTubeBypassed && !request.SkipYouTubeUrlResolving))
                {
                    logger.LogError(resultsMessage);
                }
                else
                {
                    logger.LogInformation(resultsMessage);
                }


                var episodes = podcast.Episodes.Where(x => x.Release >= indexingContext.ReleasedSince);
                foreach (var episode in episodes)
                {
                    await subjectEnricher.EnrichSubjects(
                        episode,
                        new SubjectEnrichmentOptions(
                            podcast.IgnoredAssociatedSubjects,
                            podcast.IgnoredSubjects,
                            podcast.DefaultSubject));
                }

                await podcastRepository.Save(podcast);
            }
            else
            {
                if (podcast != null)
                {
                    if (podcast.IsRemoved())
                    {
                        logger.LogWarning($"Podcast '{podcast.Name}' with id '{podcast.Id}' is removed.");
                    }
                    else
                    {
                        logger.LogWarning(
                            $"Podcast '{podcast.Name}' with id '{podcast.Id}' ignored. index-all-episodes '{podcast.IndexAllEpisodes}', episode-include-title-regex: '{podcast.EpisodeIncludeTitleRegex}'.");
                    }
                }
            }
        }

        if (!request.NoIndex)
        {
            await searchIndexerService.RunIndexer();
        }
    }
}