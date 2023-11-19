using Microsoft.Extensions.Logging;
using RedditPodcastPoster.AI;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

public class Categoriser : ICategoriser
{
    //private readonly IEpisodeClassifier _episodeClassifier;
    private readonly ILogger<Categoriser> _logger;
    private readonly ISubjectEnricher _subjectEnricher;

    public Categoriser(
        ISubjectEnricher subjectEnricher,
        //IEpisodeClassifier episodeClassifier,
        ILogger<Categoriser> logger)
    {
        _subjectEnricher = subjectEnricher;
        //_episodeClassifier = episodeClassifier;
        _logger = logger;
    }

    public async Task<bool> Categorise(Episode episode, string[]? ignoredTerms = null, string? defaultSubject = null)
    {
        var originalSubject = episode.Subjects.ToArray();
        await _subjectEnricher.EnrichSubjects(episode, new SubjectEnrichmentOptions(ignoredTerms, defaultSubject));
        var updated = !originalSubject.SequenceEqual(episode.Subjects);
        return updated;
    }
}