namespace RedditPodcastPoster.Search;

public class SearchIndexConfig
{
    public required Uri Url { get; set; }
    public required string IndexName { get; set; }
    public required string Key { get; set; }
    public required string IndexerName { get; set; }
}

