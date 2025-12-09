using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Search;

namespace RemoveEpisodes;

public class Processor(
    SearchClient searchClient,
    IPodcastRepository podcastRepository,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<Processor> logger)
{
    public async Task Process(Request request)
    {
        if (request.IsDryRun)
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

        var allSearchResultEpisodes = allSearchResults.Select(x =>
            new PodcastEpisode(x.Document.PodcastName!, x.Document.ToEpisodeModel()));
        var podcastEpisodesGroups = allSearchResultEpisodes.GroupBy(x => x.PodcastName!);
        var updatedEpisodeIds = new List<Guid>();
        foreach (var podcastEpisodeGroup in podcastEpisodesGroups)
        {
            var podcastName = podcastEpisodeGroup.Key;
            var episodes = podcastEpisodeGroup.ToArray();
            var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastName).ToListAsync();

            foreach (var podcast in podcasts)
            {
                var podcastChanged = false;
                foreach (var podcastEpisode in episodes)
                {
                    var repoPodcastEpisode = podcast.Episodes.SingleOrDefault(x => x.Id == podcastEpisode.Episode.Id);
                    if (repoPodcastEpisode != null)
                    {
                        if (!repoPodcastEpisode.Removed)
                        {
                            repoPodcastEpisode.Removed = true;
                            podcastChanged = true;
                            logger.LogInformation("Removing: '{podcastName}' - '{episodeTitle}'.",
                                podcastEpisode.PodcastName?[0..Math.Min(podcastEpisode.PodcastName.Length, 40)],
                                podcastEpisode.Episode.Title?[0..Math.Min(podcastEpisode.Episode.Title.Length, 40)]);
                        }

                        updatedEpisodeIds.Add(podcastEpisode.Episode.Id);
                    }
                    else
                    {
                        logger.LogError("Unable to find episode with episode-id {episodeId}.",
                            podcastEpisode.Episode.Id);
                    }
                }

                if (podcastChanged && !request.IsDryRun)
                {
                    await podcastRepository.Save(podcast);
                }
            }
        }

        if (updatedEpisodeIds.Any() && !request.IsDryRun)
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }
}