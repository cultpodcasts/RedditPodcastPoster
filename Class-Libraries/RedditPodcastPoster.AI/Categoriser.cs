using Microsoft.Extensions.Logging;
using RedditPodcastPoster.AI;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

public class Categoriser : ICategoriser
{
    //private readonly IEpisodeClassifier _episodeClassifier;
    private readonly ILogger<Categoriser> _logger;
    private readonly ISubjectMatcher _subjectMatcher;

    public Categoriser(
        ISubjectMatcher subjectMatcher,
        //IEpisodeClassifier episodeClassifier,
        ILogger<Categoriser> logger)
    {
        _subjectMatcher = subjectMatcher;
        //_episodeClassifier = episodeClassifier;
        _logger = logger;
    }

    public async Task<bool> Categorise(Episode episode, string[]? ignoredTerms = null)
    {
        var originalSubject = episode.Subjects.ToArray();
        await _subjectMatcher.MatchSubject(episode, ignoredTerms);
        var updated = !originalSubject.SequenceEqual(episode.Subjects);
        return updated;
    }
}