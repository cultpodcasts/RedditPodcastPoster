using System.Text.Json.Serialization;

namespace Api.Dtos;

public class Person
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("aliases")]
    public string[]? Aliases { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("blueskyHandle")]
    public string? BlueskyHandle { get; set; }
}
