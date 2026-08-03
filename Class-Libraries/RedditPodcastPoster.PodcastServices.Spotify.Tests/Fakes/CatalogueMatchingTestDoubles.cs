using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Fakes;

/// <summary>
/// Optional map from title substring to subject names; default returns no subjects.
/// </summary>
sealed class StubSubjectMatcher : ISubjectMatcher
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _titleSubstringToSubjects;

    public StubSubjectMatcher(IReadOnlyDictionary<string, IReadOnlyList<string>>? titleSubstringToSubjects = null)
    {
        _titleSubstringToSubjects = titleSubstringToSubjects ??
                                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    }

    public Task<IList<SubjectMatch>> MatchSubjects(Episode episode, SubjectEnrichmentOptions? options = null)
    {
        var title = episode.Title ?? string.Empty;
        var names = _titleSubstringToSubjects
            .Where(kv => title.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IList<SubjectMatch> matches = names
            .Select(n => new SubjectMatch(new Subject(n), []))
            .ToList();
        return Task.FromResult(matches);
    }
}

sealed class PassThroughHtmlSanitiser : RedditPodcastPoster.Text.Sanitisers.IHtmlSanitiser
{
    public string Sanitise(string htmlDescription) => htmlDescription ?? string.Empty;
}
