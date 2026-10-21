using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Twitter.Dtos;

public class Tweet
{
    [JsonPropertyName("edit_history_tweet_ids")]
    public string[] EditHistoryTweetIds { get; set; } = [];

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    /// <summary>Requested with <c>tweet.fields=entities</c>: links in <see cref="Text"/> are t.co, these are the originals.</summary>
    [JsonPropertyName("entities")]
    public TweetEntities? Entities { get; set; }

    public IEnumerable<string?> ExpandedUrls() => Entities?.Urls?.Select(x => x.ExpandedUrl) ?? [];
}

public class TweetEntities
{
    [JsonPropertyName("urls")]
    public TweetUrlEntity[]? Urls { get; set; }
}

public class TweetUrlEntity
{
    [JsonPropertyName("expanded_url")]
    public string? ExpandedUrl { get; set; }
}