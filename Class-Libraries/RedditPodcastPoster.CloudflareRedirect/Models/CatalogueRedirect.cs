namespace RedditPodcastPoster.CloudflareRedirect.Models;

/// <summary>The catalogue kinds whose public hub or page is addressed by name, as <c>/{root}/{name}</c>.</summary>
public enum CatalogueRedirectKind
{
    NewsOrganisation,
    TvShow,
    Film
}

/// <summary>
/// A rename of a news organisation, TV show or film. It is stored in the same Cloudflare KV namespace as
/// <see cref="PodcastRedirect"/>. Podcast keys are the bare old name. These keys carry the website's route root, for
/// example <c>tv/Old Name</c> → <c>New Name</c>. A podcast name cannot contain <c>/</c>, so the two never collide.
/// </summary>
public record CatalogueRedirect(CatalogueRedirectKind Kind, string OldName, string NewName)
{
    public static string RouteRoot(CatalogueRedirectKind kind) => kind switch
    {
        CatalogueRedirectKind.NewsOrganisation => "news",
        CatalogueRedirectKind.TvShow => "tv",
        CatalogueRedirectKind.Film => "film",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static string KeyFor(CatalogueRedirectKind kind, string name) => $"{RouteRoot(kind)}/{name}";

    public string Key => KeyFor(Kind, OldName);

    public string NewKey => KeyFor(Kind, NewName);
}
