using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// A curator's explicit "file this URL under that news organisation / TV show" (the website hub's Submit URL).
/// <see cref="Name"/> is only a check and a log aid; <see cref="Id"/> decides. A podcast is never created.
/// </summary>
public sealed record CatalogueParentTarget(CatalogueParentKind Kind, Guid Id, string? Name = null);

public class SubmitParentNotFoundException(CatalogueParentKind kind, Guid parentId)
    : Exception($"No {kind} exists for id '{parentId}'.")
{
    public CatalogueParentKind Kind { get; } = kind;
    public Guid ParentId { get; } = parentId;
}
