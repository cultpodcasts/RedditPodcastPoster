using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Twitter.Dtos;

public class Tweet
{
    [JsonPropertyName("edit_history_tweet_ids")]
    public string[] EditHistoryTweetIds { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}