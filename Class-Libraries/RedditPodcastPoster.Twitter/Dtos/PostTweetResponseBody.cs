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
/// <c>DELETE /2/tweets/:id</c> success body for the deleted flag only:
/// <c>{"data":{"deleted":true}}</c>. Problem / errors shapes use <see cref="Clients.XApiProblem"/>.
/// </summary>
public class DeleteTweetResponseBody
{
    [JsonPropertyName("data")]
    public DeleteTweetResponseData? Data { get; set; }
}

public class DeleteTweetResponseData
{
    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}
