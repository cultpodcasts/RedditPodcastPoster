using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace Index;

internal class IndexProcessor(
    IPodcastRepository podcastRepository,
    IPodcastUpdater podcastUpdater,
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
            podcastIds = await podcastRepository.GetAllIds().ToArrayAsync();
        }

        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null &&
                (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))

            {
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
        }
    }
}