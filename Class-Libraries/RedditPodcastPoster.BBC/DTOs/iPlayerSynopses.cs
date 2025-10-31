using System.Text.Json.Serialization;

public class iPlayerSynopses
{
    [JsonPropertyName("small")]
    public string? Short { get; set; }

    [JsonPropertyName("medium")]
    public string? Medium { get; set; }

    [JsonPropertyName("large")]
    public string? Long { get; set; }

    public string Description => Long ?? Medium ?? Short ?? string.Empty;
}