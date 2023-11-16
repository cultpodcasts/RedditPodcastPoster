using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesProcessor
{
    private readonly ILogger<CategorisePodcastEpisodesProcessor> _logger;
    private readonly IPodcastRepository _repository;
    private readonly ISubjectMatcher _subjectMatcher;

    public CategorisePodcastEpisodesProcessor(
        IPodcastRepository repository,
        ISubjectMatcher subjectMatcher,
        ILogger<CategorisePodcastEpisodesProcessor> logger)
    {
        _repository = repository;
        _subjectMatcher = subjectMatcher;
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
                await _subjectMatcher.MatchSubject(
                    podcastEpisode,
                    podcast.IgnoredAssociatedSubjects,
                    podcast.DefaultSubject);
            }

            if (request.Commit)
            {
                await _repository.Save(podcast);
            }
        }
    }
}