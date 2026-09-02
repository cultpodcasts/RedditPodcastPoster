namespace RedditPodcastPoster.UrlSubmission.Models;

public class AmbiguousPodcastNameException(string podcastName, IReadOnlyList<Guid> podcastIds)
    : Exception($"Multiple podcasts share the name '{podcastName}'. Submit with podcastId after the curator chooses.")
{
    public string PodcastName { get; } = podcastName;

    public IReadOnlyList<Guid> PodcastIds { get; } = podcastIds;
}
