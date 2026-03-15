using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Indexing;

public class Indexer(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IPodcastUpdater podcastUpdater,
    ISubjectEnricher subjectEnricher,
    ILogger<Indexer> logger
) : IIndexer
{
    public async Task<IndexResponse> Index(string podcastName, IndexingContext indexingContext)
    {
        var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastName).ToListAsync();

        if (podcasts.Any())
        {
            var canIndex = podcasts.Where(x =>
                !(x.Removed.HasValue && x.Removed.Value) &&
                (x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex))).ToArray();
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

            var podcast = canIndex.Single();
            return await Index(podcast.Id,
                indexingContext with { SkipShortEpisodes = !podcast.BypassShortEpisodeChecking ?? false });
        }

        return new IndexResponse(IndexStatus.NotFound);
    }

    public async Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext, bool forceIndex = false)
    {
        var podcast = await podcastRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            return new IndexResponse(IndexStatus.NotFound);
        }

        if (podcast.Removed == true)
        {
            logger.LogWarning("Podcast '{PodcastName}' with id '{PodcastId}' is removed and will not be indexed.",
                podcast.Name,
                podcast.Id);
            return new IndexResponse(IndexStatus.NotPerformed);
        }

        IndexStatus status;
        IndexedEpisode[]? updatedEpisodes = null;

        var performAutoIndex = podcast.IndexAllEpisodes ||
                               !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex);
        var enrichOnly = false;
        if (!performAutoIndex)
        {
            var hasEpisodesToEnrich = await HasEpisodesAwaitingEnrichment(podcastId, indexingContext);
            if (hasEpisodesToEnrich)
            {
                enrichOnly = true;
            }
        }

        if (performAutoIndex || enrichOnly || forceIndex)
        {
            logger.LogInformation("Indexing podcast '{podcastName}' with podcast-id '{podcastId}'.", podcast.Name,
                podcastId);

            // Pass V2 podcast directly - no conversion needed!
            var results = await podcastUpdater.Update(podcast, enrichOnly, indexingContext);

            updatedEpisodes = results.MergeResult.AddedEpisodes
                .Select(x =>
                    new IndexedEpisode(
                        x,
                        x.Urls.Spotify != null,
                        x.Urls.Apple != null,
                        x.Urls.YouTube != null))
                .Concat(results.EnrichmentResult.UpdatedEpisodes.Select(x =>
                    new IndexedEpisode(
                        x.Episode,
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

            foreach (var indexedEpisode in updatedEpisodes)
            {
                var subjectsResult = await subjectEnricher.EnrichSubjects(
                    indexedEpisode.Episode,
                    new SubjectEnrichmentOptions(
                        podcast.IgnoredAssociatedSubjects,
                        podcast.IgnoredSubjects,
                        podcast.DefaultSubject,
                        podcast.DescriptionRegex));

                var result = updatedEpisodes.FirstOrDefault(x => x.Episode.Id == indexedEpisode.Episode.Id);
                result?.Subjects = subjectsResult.Additions;

                if (subjectsResult.Additions.Any() || subjectsResult.Removals.Any())
                {
                    await episodeRepository.Save(indexedEpisode.Episode);
                }
            }

            status = IndexStatus.Performed;
        }
        else
        {
            logger.LogWarning(
                "Podcast '{podcastName}' with id '{podcastId}' ignored. index-all-episodes '{podcastIndexAllEpisodes}', episode-include-title-regex: '{podcastEpisodeIncludeTitleRegex}'.",
                podcast.Name, podcast.Id, podcast.IndexAllEpisodes, podcast.EpisodeIncludeTitleRegex);

            status = IndexStatus.NotPerformed;
        }

        updatedEpisodes = Collapse(updatedEpisodes);

        return new IndexResponse(status, updatedEpisodes);
    }

    private async Task<bool> HasEpisodesAwaitingEnrichment(Guid podcastId, IndexingContext indexingContext)
    {
        // Use IEpisodeRepository instead of legacy repository method
        var episodes = await episodeRepository.GetByPodcastId(podcastId)
            .Where(x => x.Release >= indexingContext.ReleasedSince)
            .ToListAsync();

        return episodes.Any();
    }

    private static IndexedEpisode[]? Collapse(IndexedEpisode[]? episodes)
    {
        if (episodes == null)
        {
            return null;
        }

        var results = new List<IndexedEpisode>();
        var groupedEpisodes = episodes.GroupBy(x => x.Episode.Id);
        foreach (var groupedEpisode in groupedEpisodes)
        {
            var items = groupedEpisode.ToList();
            var result = new IndexedEpisode(
                items.First().Episode,
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