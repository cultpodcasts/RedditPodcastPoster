using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Twitter.Dtos;

public class GetTweetsResponse
{
    [JsonPropertyName("meta")]
    public Meta MetaData { get; set; }

    [JsonPropertyName("data")]
    public Tweet[] Tweets { get; set; }
}