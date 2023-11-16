using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectService
{
    Task<Subject?> Match(Subject subject);
    Task<Subject?> Match(string subject);
    Task<IEnumerable<SubjectMatch>> Match(Episode episode, string[]? ignoredTerms = null);
}