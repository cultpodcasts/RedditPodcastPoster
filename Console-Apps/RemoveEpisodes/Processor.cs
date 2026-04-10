using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Search;

namespace RemoveEpisodes;

public class Processor(
    SearchClient searchClient,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<Processor> logger)
{
    public async Task Process(Request request)
    {
        if (request.IsNonDryRun)
        {
            logger.LogWarning("Is real-run.");
        }
        else
        {
            logger.LogWarning("Is Dry-run.");
        }

        var options = new SearchOptions();
        options.Select.Add("id");
        options.Select.Add("episodeTitle");
        options.Select.Add("episodeDescription");
        options.Select.Add("subjects");
        options.Select.Add("podcastName");
        var searchQuery = request.NotWholeTerm ? request.Query : $"\"{request.Query}\"";
        var results = await searchClient.SearchAsync<SearchDocument>(searchQuery, options);
        if (results == null)
        {
            throw new InvalidOperationException("Results are null");
        }

        var searchResults = results.Value;
        var allSearchResults = await searchResults.GetResultsAsync().ToListAsync();
        if (allSearchResults == null)
        {
            throw new InvalidOperationException("All Search Results is null");
        }

        var message = $"Episodes matching query: {allSearchResults.Count}, throttled at {request.Throttle}.";
        if (allSearchResults.Count > request.Throttle)
        {
            logger.LogError(message);
            return;
        }

        logger.LogInformation(message);

        var allSearchResultEpisodes = allSearchResults
            .Where(x => !string.IsNullOrWhiteSpace(x.Document.PodcastName))
            .Select(x => new
            {
                PodcastName = x.Document.PodcastName!,
                EpisodeId = x.Document.Id,
                EpisodeTitle = x.Document.EpisodeTitle
            });

        var podcastEpisodesGroups = allSearchResultEpisodes.GroupBy(x => x.PodcastName);
        var updatedEpisodeIds = new List<Guid>();

        foreach (var podcastEpisodeGroup in podcastEpisodesGroups)
        {
            var podcastName = podcastEpisodeGroup.Key;
            var episodes = podcastEpisodeGroup.ToArray();
            var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastName).ToListAsync();

            foreach (var podcast in podcasts)
            {
                foreach (var podcastEpisode in episodes)
                {
                    var repoEpisode = await episodeRepository.GetEpisode(
                        podcast.Id,
                        podcastEpisode.EpisodeId);

                    if (repoEpisode != null)
                    {
                        if (!repoEpisode.Removed)
                        {
                            repoEpisode.Removed = true;
                            logger.LogInformation("Removing: '{podcastName}' - '{episodeTitle}'.",
                                podcastEpisode.PodcastName[0..Math.Min(podcastEpisode.PodcastName.Length, 40)],
                                podcastEpisode.EpisodeTitle[0..Math.Min(podcastEpisode.EpisodeTitle.Length, 40)]);

                            if (request.IsNonDryRun)
                            {
                                await episodeRepository.Save(repoEpisode);
                            }
                        }

                        updatedEpisodeIds.Add(podcastEpisode.EpisodeId);
                    }
                    else
                    {
                        logger.LogError("Unable to find episode with episode-id {episodeId} in podcast-id {podcastId}.",
                            podcastEpisode.EpisodeId, podcast.Id);
                    }
                }
            }
        }

        if (updatedEpisodeIds.Any() && request.IsNonDryRun)
        {
            var result = await searchClient.DeleteDocumentsAsync(
                "id",
                updatedEpisodeIds.Select(x => x.ToString()),
                new IndexDocumentsOptions { ThrowOnAnyError = false });
            var success = result.Value.Results.Any(x => x.Succeeded);
            if (!success)
            {
                logger.LogError("Error deleting documents from search-index: {errorMessages}. Ids: {ids}.",
                    string.Join(", ",
                        result.Value.Results.Where(x => !x.Succeeded).Select(x => x.ErrorMessage).Distinct()
                            .Select(x => $"'{x}'")),
                    string.Join(", ",
                        result.Value.Results.Select(x => $"'{x.Key}'")));
            }
        }
    }
}