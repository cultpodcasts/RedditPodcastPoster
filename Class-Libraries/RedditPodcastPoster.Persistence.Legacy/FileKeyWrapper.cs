using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Persistence.Legacy;

public class FileKeyWrapper
{
    [JsonPropertyName("fileKey")]
    public required string FileKey { get; set; }
}