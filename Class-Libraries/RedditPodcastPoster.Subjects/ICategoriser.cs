using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public interface ICategoriser
{
    public Task<bool> Categorise(
        Episode episode,
        string[]? ignoredAssociatedSubjects,
        string[]? ignoredSubjects,
        string? defaultSubject,
        string descriptionRegex);
}