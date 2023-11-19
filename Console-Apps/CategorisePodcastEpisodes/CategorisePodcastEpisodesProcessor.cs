using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesProcessor
{
    private readonly ILogger<CategorisePodcastEpisodesProcessor> _logger;
    private readonly IPodcastRepository _repository;
    private readonly ISubjectEnricher _subjectEnricher;

    public CategorisePodcastEpisodesProcessor(
        IPodcastRepository repository,
        ISubjectEnricher subjectEnricher,
        ILogger<CategorisePodcastEpisodesProcessor> logger)
    {
        _repository = repository;
        _subjectEnricher = subjectEnricher;
        _logger = logger;
    }

    public async Task Run(CategorisePodcastEpisodesRequest request)
    {
        var podcastIds = request.PodcastIds.Split(",");
        foreach (var podcastId in podcastIds)
        {
            var podcast = await _repository.GetPodcast(Guid.Parse(podcastId));
            _logger.LogInformation($"Processing '{podcastId}' : '{podcast.Name}'.");
            if (podcast == null)
            {
                throw new ArgumentException($"No podcast with id '{podcastId}' found.");
            }

            foreach (var podcastEpisode in podcast.Episodes)
            {
                if (request.ResetSubjects)
                {
                    podcastEpisode.Subjects = new List<string>();
                }

                await _subjectEnricher.EnrichSubjects(
                    podcastEpisode,
                    new SubjectEnrichmentOptions(
                        podcast.IgnoredAssociatedSubjects,
                        podcast.DefaultSubject));
            }

            if (request.Commit)
            {
                await _repository.Save(podcast);
            }
        }
    }
}