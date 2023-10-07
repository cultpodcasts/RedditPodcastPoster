using System.Text.Json.Serialization;

namespace Indexer.Models;

public class ScalarResult<T>
{
    [JsonPropertyName("$1")]
    public T item { get; set; }

}