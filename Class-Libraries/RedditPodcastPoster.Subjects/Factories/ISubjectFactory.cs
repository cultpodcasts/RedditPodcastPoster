using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects.Factories;

public interface ISubjectFactory
{
    Task<Subject> Create(
        string subjectName,
        string? aliases = null,
        string? associatedSubjects = null,
        string? hashTags = null);
}