using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.AI;

public interface ICategoriser
{
    public Task<bool> Categorise(
        Episode episode,
        string[]? ignoredTerms = null,
        string? defaultSubject = null);
}