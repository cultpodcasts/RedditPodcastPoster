using System.Text.Json.Serialization;

namespace API.Models;

public class DurationResult
{
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

}