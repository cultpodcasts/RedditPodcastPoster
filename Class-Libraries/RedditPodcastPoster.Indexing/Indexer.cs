using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace RedditPodcastPoster.Indexing;

public class Indexer(
    IPodcastRepository podcastRepository,
    IPodcastUpdater podcastUpdater,
    ISubjectEnricher subjectEnricher,
    ILogger<Indexer> logger
) : IIndexer
{
    public async Task<IndexResponse> Index(string podcastName, IndexingContext indexingContext)
    {
        var podcast = await podcastRepository.GetBy(x => x.Name == podcastName, x => new {id = x.Id, name = x.Name});
        if (podcast != null)
        {
            return await Index(podcast.id, indexingContext);
        }

        return new IndexResponse(IndexStatus.NotFound);
    }

    public async Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext)
    {
        IndexStatus status= IndexStatus.Unset;
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast != null && !podcast.IsRemoved() &&
            (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))
        {
            logger.LogInformation($"Indexing podcast {podcast.Name}' with podcast-id '{podcastId}'.");
            var results = await podcastUpdater.Update(podcast, indexingContext);
            var resultsMessage = results.ToString();
            if (results.MergeResult.FailedEpisodes.Any() ||
                (results.SpotifyBypassed && !indexingContext.SkipSpotifyUrlResolving) ||
                (results.YouTubeBypassed && !indexingContext.SkipYouTubeUrlResolving))
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
            status = IndexStatus.Performed;
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

                status = IndexStatus.NotPerformed;
            }
            else
            {
                status = IndexStatus.NotFound;
            }
        }

        return new IndexResponse(status);
    }
}