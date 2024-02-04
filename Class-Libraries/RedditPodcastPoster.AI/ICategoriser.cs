using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.AI;

public interface ICategoriser
{
    public Task<bool> Categorise(
        Episode episode,
        string[]? ignoredAssociatedSubjects = null,
        string[]? ignoredSubjects = null,
        string? defaultSubject = null);
}