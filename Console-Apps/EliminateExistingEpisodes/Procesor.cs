using System.Text.RegularExpressions;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text.EliminationTerms;

namespace EliminateExistingEpisodes;

public class Processor(
    IPodcastRepository repository,
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
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                x => x.Id).ToListAsync();
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
        var eliminationTermsProvider = await eliminationTermsProviderInstance.GetAsync();
        var eliminationTerms = eliminationTermsProvider.GetEliminationTerms();
        var filterResult = podcastFilter.Filter(podcast, eliminationTerms.Terms);
        logger.LogInformation(filterResult.ToString());
        foreach (var episode in filterResult.FilteredEpisodes)
        {
            await DeleteSearchDocument(episode.Episode);
        }

        if (!string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex, Podcast.EpisodeIncludeTitleFlags);
            foreach (var episode in podcast.Episodes.Where(x => !x.Removed))
            {
                if (!includeEpisodeRegex.IsMatch(episode.Title))
                {
                    episode.Removed = true;
                    logger.LogInformation(
                        "Removing episode '{episodeTitle}' of podcast '{podcastName}' due to mismatch with '{episodeIncludeTitleRegex}'.",
                        episode.Title, podcast.Name, podcast.EpisodeIncludeTitleRegex);
                    await DeleteSearchDocument(episode);
                }
            }
        }

        await repository.Save(podcast);
    }

    private async Task DeleteSearchDocument(Episode episode)
    {
        var result = await searchClient.DeleteDocumentsAsync(
            "id",
            [episode.Id.ToString()],
            new IndexDocumentsOptions { ThrowOnAnyError = true },
            CancellationToken.None);
        var success = result.Value.Results.First().Succeeded;
        if (!success)
        {
            logger.LogError("Error removing search-item with episode-id '{episodeId}', message: '{mesage}'.",
                episode.Id, result.Value.Results.First().ErrorMessage);
        }
    }
}