using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;

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
        var podcasts = await podcastRepository.GetAllBy(
            x => x.Name == podcastName,
            x => new
            {
                id = x.Id, name = x.Name,
                indexAllEpisodes = x.IndexAllEpisodes,
                episodeIncludeTitleRegex = x.EpisodeIncludeTitleRegex,
                removed = x.Removed
            }).ToListAsync();

        if (podcasts.Any())
        {
            var canIndex = podcasts.Where(x =>
                !(x.removed.HasValue && x.removed.Value) &&
                (x.indexAllEpisodes || !string.IsNullOrWhiteSpace(x.episodeIncludeTitleRegex)));
            if (!canIndex.Any() || canIndex.Count() > 1)
            {
                if (podcasts.Count == 1 && !canIndex.Any())
                {
                    canIndex = [podcasts.Single()];
                }
                else
                {
                    return new IndexResponse(IndexStatus.NotPerformed);
                }
            }

            return await Index(canIndex.Single().id, indexingContext);
        }

        return new IndexResponse(IndexStatus.NotFound);
    }

    public async Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext)
    {
        IndexStatus status;
        var podcast = await podcastRepository.GetPodcast(podcastId);
        IndexedEpisode[]? updatedEpisodes = null;

        var performAutoIndex = podcast != null &&
                               !podcast.IsRemoved() &&
                               (podcast.IndexAllEpisodes ||
                                !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex));
        var enrichOnly = false;
        if (!performAutoIndex)
        {
            var hasEpisodesToEnrich = await HasEpisodesAwaitingEnrichment(podcastId, indexingContext);
            if (hasEpisodesToEnrich)
            {
                enrichOnly = true;
            }
        }

        if (performAutoIndex || enrichOnly)
        {
            logger.LogInformation($"Indexing podcast {podcast.Name}' with podcast-id '{podcastId}'.");
            var results = await podcastUpdater.Update(podcast, enrichOnly, indexingContext);

            updatedEpisodes = results.MergeResult.AddedEpisodes
                .Select(x =>
                    new IndexedEpisode(
                        x.Id,
                        x.Urls.Spotify != null,
                        x.Urls.Apple != null,
                        x.Urls.YouTube != null))
                .Concat(results.EnrichmentResult.UpdatedEpisodes.Select(x =>
                    new IndexedEpisode(
                        x.Episode.Id,
                        x.EnrichmentContext.SpotifyUrlUpdated,
                        x.EnrichmentContext.AppleUrlUpdated,
                        x.EnrichmentContext.YouTubeUrlUpdated)))
                .Distinct()
                .ToArray();

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
                var subjectsResult = await subjectEnricher.EnrichSubjects(
                    episode,
                    new SubjectEnrichmentOptions(
                        podcast.IgnoredAssociatedSubjects,
                        podcast.IgnoredSubjects,
                        podcast.DefaultSubject,
                        podcast.DescriptionRegex));
                var result = updatedEpisodes.FirstOrDefault(x => x.EpisodeId == episode.Id);
                if (result != null)
                {
                    result.Subjects = subjectsResult.Additions;
                }
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

        updatedEpisodes = Collapse(updatedEpisodes);

        return new IndexResponse(status, updatedEpisodes);
    }

    private async Task<bool> HasEpisodesAwaitingEnrichment(Guid podcastId, IndexingContext indexingContext)
    {
        if (indexingContext.ReleasedSince == null)
        {
            return false;
        }

        return await podcastRepository.PodcastHasEpisodesAwaitingEnrichment(
            podcastId,
            indexingContext.ReleasedSince.Value);
    }

    private static IndexedEpisode[]? Collapse(IndexedEpisode[]? episodes)
    {
        if (episodes == null)
        {
            return null;
        }

        var results = new List<IndexedEpisode>();
        var groupedEpisodes = episodes.GroupBy(x => x.EpisodeId);
        foreach (var groupedEpisode in groupedEpisodes)
        {
            var items = groupedEpisode.ToList();
            var result = new IndexedEpisode(
                items.First().EpisodeId,
                items.Any(x => x.Spotify),
                items.Any(x => x.Apple),
                items.Any(x => x.YouTube));
            var subjects = items.SelectMany(x => x.Subjects).Distinct();
            result.Subjects = subjects.ToArray();
            results.Add(result);
        }

        return results.ToArray();
    }
}