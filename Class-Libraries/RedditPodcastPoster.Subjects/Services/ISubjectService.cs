using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects.Services;

public interface ISubjectService
{
    Task<Subject?> Match(Subject subject);
    Task<Subject?> Match(string subject);

    Task<IEnumerable<SubjectMatch>> Match(
        Episode episode,
        string[]? ignoredAssociatedSubjects = null,
        string[]? ignoredSubjects = null,
        string? descriptionRegex = null);
}