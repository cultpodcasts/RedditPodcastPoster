using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ISubjectService
{
    Task<Subject?> Match(Subject subject);
    Task<Subject?> Match(string subject);
}