using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Categorisation;

public interface ICategoriser
{
    public Task<bool> Categorise(
        Episode episode,
        string[]? ignoredAssociatedSubjects,
        string[]? ignoredSubjects,
        string? defaultSubject,
        string descriptionRegex);
}