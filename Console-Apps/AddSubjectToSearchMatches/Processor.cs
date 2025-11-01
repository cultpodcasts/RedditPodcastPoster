using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;

namespace AddSubjectToSearchMatches;

public class Processor(
    ISubjectsProvider subjectsProvider,
    SearchClient searchClient,
    ISubjectMatcher subjectMatcher,
    IPodcastRepository podcastRepository,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<Processor> logger)
{
    public async Task Process(Request request)
    {
        var subjects = subjectsProvider.GetAll();
        if (await subjects.AllAsync(x => x.Name != request.Query))
        {
            throw new InvalidOperationException($"No subject with name '{request.Query}' found.");
        }

        var options = new SearchOptions();
        options.Select.Add("id");
        options.Select.Add("episodeTitle");
        options.Select.Add("episodeDescription");
        options.Select.Add("subjects");
        options.Select.Add("podcastName");
        var results = await searchClient.SearchAsync<SearchDocument>(request.Query, options);
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
                    if (repoPodcastEpisode != null && !repoPodcastEpisode.Subjects.Contains(request.Query))
                    {
                        var subjectEnrichmentOptions = new SubjectEnrichmentOptions(
                            podcast.IgnoredAssociatedSubjects,
                            podcast.IgnoredSubjects,
                            podcast.DefaultSubject,
                            podcast.DescriptionRegex);
                        var subjectMatches = await subjectMatcher.MatchSubjects(
                            podcastEpisode.Episode,
                            subjectEnrichmentOptions
                        );
                        if (subjectMatches.Any(x => x.Subject.Name == request.Query) &&
                            !repoPodcastEpisode.Subjects.Contains(request.Query))
                        {
                            updatedEpisodeIds.Add(repoPodcastEpisode.Id);
                            podcastChanged = true;
                            repoPodcastEpisode.Subjects.Add(request.Query);
                            logger.LogWarning(
                                $"Podcast '{podcastName}' episode '{repoPodcastEpisode.Id}' has subject added.");
                        }
                    }
                    else if (repoPodcastEpisode != null &&
                             repoPodcastEpisode.Subjects.Count(x => x == request.Query) > 1)
                    {
                        logger.LogWarning(
                            $"Podcast '{podcastName}' episode '{repoPodcastEpisode.Id}' has subject more than once.");
                    }
                }

                if (podcastChanged)
                {
                    await podcastRepository.Save(podcast);
                }
            }
        }

        if (updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }
}