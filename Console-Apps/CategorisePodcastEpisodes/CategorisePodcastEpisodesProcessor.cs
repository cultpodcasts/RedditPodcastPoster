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
        var podcast = await _repository.GetPodcast(request.PodcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast with id '{request.PodcastId}' found.");
        }

        foreach (var podcastEpisode in podcast.Episodes)
        {
            await _subjectMatcher.MatchSubject(podcastEpisode, podcast.IgnoredAssociatedSubjects);
        }

        await _repository.Save(podcast);
    }
}