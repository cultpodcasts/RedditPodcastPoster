using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesProcessor
{
    private readonly ILogger<CategorisePodcastEpisodesProcessor> _logger;
    private readonly IPodcastRepository _repository;
    private readonly ISubjectService _subjectService;

    public CategorisePodcastEpisodesProcessor(
        IPodcastRepository repository,
        ISubjectService subjectService,
        ILogger<CategorisePodcastEpisodesProcessor> logger)
    {
        _repository = repository;
        _subjectService = subjectService;
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
            var subjectMatches = await _subjectService.Match(podcastEpisode, true, podcast.IgnoredAssociatedSubjects);
            var subjectMatch = subjectMatches.GroupBy(x => x.MatchResults.Sum(y => y.Matches)).MaxBy(x => x.Key);
            if (subjectMatch != null)
            {
                _logger.LogInformation(
                    $"{subjectMatch.Count()} - {string.Join(",", subjectMatch.Select(x => "'" + x.Subject.Name + "' (" + x.MatchResults.MaxBy(x => x.Matches)?.Term + ")"))} : '{podcastEpisode.Title}'.");
                podcastEpisode.Subject = string.Join(", ", subjectMatch.Select(x => x.Subject.Name));
            }
            else
            {
                _logger.LogInformation($"'No match: '{podcastEpisode.Title}'.");
            }
        }

        await _repository.Save(podcast);
    }
}