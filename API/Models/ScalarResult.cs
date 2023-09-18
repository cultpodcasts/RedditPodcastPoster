using System.Text.Json.Serialization;

namespace API.Models;

public class ScalarResult<T>
{
    [JsonPropertyName("$1")]
    public T item { get; set; }

}