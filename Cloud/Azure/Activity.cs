using System.Text.Json.Serialization;

namespace Azure;

public sealed class Activity
{
    public static string PartitionKey = nameof(Activity);

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }

    [JsonPropertyName("operationType")]
    public required string OperationType { get; set; }
}