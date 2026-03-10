using System.Text.RegularExpressions;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.EliminationTerms;

namespace EliminateExistingEpisodes;

public class Processor(
    IPodcastRepositoryV2 repository,
    IEpisodeRepository episodeRepository,
    IPodcastFilter podcastFilter,
    IAsyncInstance<IEliminationTermsProvider> eliminationTermsProviderInstance,
    SearchClient searchClient,
    ILogger<Processor> logger)
{
    public async Task Run(Request request)
    {
        Guid podcastId;
        if (request.PodcastId.HasValue)
        {
            podcastId = request.PodcastId.Value;
        }
        else if (request.PodcastName != null)
        {
            var podcastIds = await repository.GetAllBy(x =>
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Id)
                .ToListAsync();
            if (!podcastIds.Any())
            {
                throw new InvalidOperationException($"No podcast matching '{request.PodcastName}' could be found.");
            }

            if (podcastIds.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple podcasts matching '{request.PodcastName}' were found. Ids: {string.Join(", ", podcastIds)}.");
            }

            podcastId = podcastIds.First();
        }
        else
        {
            throw new InvalidOperationException("A podcast-id or podcast-name must be provided.");
        }

        var podcast = await repository.GetBy(x => x.Id == podcastId);
        if (podcast == null)
        {
            throw new InvalidOperationException($"Podcast with id '{podcastId}' not found.");
        }

        var episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();

        var eliminationTermsProvider = await eliminationTermsProviderInstance.GetAsync();
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(podcast, episodes, eliminationTerms.Terms);
        logger.LogInformation(filterResult.ToString());
        foreach (var filteredEpisode in filterResult.FilteredEpisodes)
        {
            await DeleteSearchDocument(filteredEpisode.Episode.Id);
        }

        if (!string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex, Podcast.EpisodeIncludeTitleFlags);
            foreach (var episode in episodes.Where(x => !x.Removed))
            {
                if (!includeEpisodeRegex.IsMatch(episode.Title))
                {
                    episode.Removed = true;
                    logger.LogInformation(
                        "Removing episode '{episodeTitle}' of podcast '{podcastName}' due to mismatch with '{episodeIncludeTitleRegex}'.",
                        episode.Title, podcast.Name, podcast.EpisodeIncludeTitleRegex);
                    await DeleteSearchDocument(episode.Id);
                }
            }
        }

        var episodesById = episodes.ToDictionary(x => x.Id);
        foreach (var filteredEpisode in filterResult.FilteredEpisodes)
        {
            if (episodesById.TryGetValue(filteredEpisode.Episode.Id, out var detachedEpisode))
            {
                detachedEpisode.Removed = true;
                await episodeRepository.Save(detachedEpisode);
            }
        }

        foreach (var serviceEpisode in episodes)
        {
            if (serviceEpisode.Removed &&
                episodesById.TryGetValue(serviceEpisode.Id, out var detachedEpisode) &&
                !detachedEpisode.Removed)
            {
                detachedEpisode.Removed = true;
                await episodeRepository.Save(detachedEpisode);
            }
        }
    }

    private async Task DeleteSearchDocument(Guid episodeId)
    {
        var result = await searchClient.DeleteDocumentsAsync(
            "id",
            [episodeId.ToString()],
            new IndexDocumentsOptions { ThrowOnAnyError = true },
            CancellationToken.None);
        var success = result.Value.Results.First().Succeeded;
        if (!success)
        {
            logger.LogError("Error removing search-item with episode-id '{episodeId}', message: '{mesage}'.",
                episodeId, result.Value.Results.First().ErrorMessage);
        }
    }

}