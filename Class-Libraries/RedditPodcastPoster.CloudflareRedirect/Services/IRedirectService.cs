using RedditPodcastPoster.CloudflareRedirect.Models;

namespace RedditPodcastPoster.CloudflareRedirect.Services;

public interface IRedirectService
{
    public Task<bool> CreatePodcastRedirect(PodcastRedirect podcastRedirect);

    /// <summary>
    /// Records <c>{root}/{old}</c> → <c>{new}</c>, with the same safety rule as <see cref="CreatePodcastRedirect"/>.
    /// Returns false, and writes nothing, when an existing redirect of the same kind starts at the new name, because
    /// that would make a loop. Throws when the KV write itself fails.
    /// </summary>
    public Task<bool> CreateCatalogueRedirect(CatalogueRedirect redirect);
}
