using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Twitter.Dtos;

/// <summary><c>POST /2/tweets</c> success body: <c>{"data":{"id":"…","text":"…"}}</c>.</summary>
public class PostTweetResponseBody
{
    [JsonPropertyName("data")]
    public PostTweetResponseData? Data { get; set; }
}

public class PostTweetResponseData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

/// <summary>
/// <c>DELETE /2/tweets/:id</c> body: <c>{"data":{"deleted":true}}</c>, <c>errors</c>, or (non-2xx) an X problem
/// (<c>application/problem+json</c>) with a top-level <c>type</c> and <c>title</c>.
/// </summary>
public class DeleteTweetResponseBody
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("data")]
    public DeleteTweetResponseData? Data { get; set; }

    [JsonPropertyName("errors")]
    public DeleteTweetResponseError[]? Errors { get; set; }
}

public class DeleteTweetResponseData
{
    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}

public class DeleteTweetResponseError
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
