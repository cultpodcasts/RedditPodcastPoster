using RedditPodcastPoster.EntitySearchIndexer.Models;

namespace Api.Models;

public class EpisodeUpdateOutcome
{
    public bool? TweetDeleted { get; set; }
    public bool? BlueskyPostDeleted { get; set; }
    public EntitySearchIndexerResponse? SearchIndexer { get; set; }
}
