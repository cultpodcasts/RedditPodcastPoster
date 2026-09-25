namespace RedditPodcastPoster.UrlSubmission.Models;

public class AmbiguousParentNameException(string contentKind, string parentName, IReadOnlyList<Guid> parentIds)
    : Exception(
        $"Multiple {contentKind} parents share the name '{parentName}'. Submit after the curator chooses.")
{
    public string ContentKind { get; } = contentKind;

    public string ParentName { get; } = parentName;

    public IReadOnlyList<Guid> ParentIds { get; } = parentIds;
}
