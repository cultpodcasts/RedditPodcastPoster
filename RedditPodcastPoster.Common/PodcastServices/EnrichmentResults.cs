namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentResults
{
    public IList<EnrichmentResult> UpdatedEpisodes { get; set; } = new List<EnrichmentResult>();
}