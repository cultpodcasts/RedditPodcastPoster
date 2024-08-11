using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodePublishResponse
{
    [JsonPropertyName("posted")]
    public bool? Posted { get; set; }

    [JsonPropertyName("tweeted")]
    public bool? Tweeted { get; set; }


    public bool Updated()
    {
        return (Posted.HasValue && Posted.Value) || (Tweeted.HasValue && Tweeted.Value);
    }
}