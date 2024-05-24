using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IPodcastUpdater podcastUpdater,
    ISubjectEnricher subjectEnricher,
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
            podcastIds = await podcastRepository.GetAllIds().ToArrayAsync();
        }

        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null && !podcast.IsRemoved() &&
                (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))

            {
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
                if (podcast != null && podcast.IsRemoved())
                {
                    logger.LogWarning($"Podcast with id '{podcast.Id}' is removed.");
                }
            }
        }
    }
}