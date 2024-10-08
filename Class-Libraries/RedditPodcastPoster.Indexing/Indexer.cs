﻿using Microsoft.Extensions.Logging;
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
                (x.indexAllEpisodes ||
                 !string.IsNullOrWhiteSpace(x.episodeIncludeTitleRegex)));
            if (!canIndex.Any() || canIndex.Count() > 1)
            {
                return new IndexResponse(IndexStatus.NotPerformed);
            }

            return await Index(canIndex.Single().id, indexingContext);
        }

        return new IndexResponse(IndexStatus.NotFound);
    }

    public async Task<IndexResponse> Index(Guid podcastId, IndexingContext indexingContext)
    {
        IndexStatus status;
        var podcast = await podcastRepository.GetPodcast(podcastId);
        Guid[]? updatedEpisodeIds = null;
        if (podcast != null && !podcast.IsRemoved() &&
            (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex)))
        {
            logger.LogInformation($"Indexing podcast {podcast.Name}' with podcast-id '{podcastId}'.");
            var results = await podcastUpdater.Update(podcast, indexingContext);

            updatedEpisodeIds = results.MergeResult.AddedEpisodes.Select(x => x.Id)
                .Concat(results.EnrichmentResult.UpdatedEpisodes.Select(x => x.Episode.Id)).Distinct().ToArray();

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

        return new IndexResponse(status, updatedEpisodeIds);
    }
}