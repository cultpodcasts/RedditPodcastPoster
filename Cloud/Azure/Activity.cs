using System.Text.Json.Serialization;

namespace Azure;

public sealed class Activity
{
    public static string PartitionKey = nameof(Activity);

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; }
}