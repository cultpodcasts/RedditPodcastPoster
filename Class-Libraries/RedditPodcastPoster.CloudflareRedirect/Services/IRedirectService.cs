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

    /// <summary>
    /// Read-only form of the <see cref="CreateCatalogueRedirect"/> loop rule, so a caller can refuse a rename before
    /// writing anything and write the redirect last.
    /// </summary>
    public Task<bool> IsCatalogueRedirectSafe(CatalogueRedirect redirect);
}
