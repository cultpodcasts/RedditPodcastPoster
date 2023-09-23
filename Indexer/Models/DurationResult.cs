using System.Text.Json.Serialization;

namespace Indexer.Models;

public class DurationResult
{
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

}