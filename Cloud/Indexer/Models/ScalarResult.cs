using System.Text.Json.Serialization;

namespace Indexer.Models;

public class ScalarResult<T>
{
    [JsonPropertyName("$1")]
    public required T Item { get; set; }

}