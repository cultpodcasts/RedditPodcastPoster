using System.Text.Json.Serialization;

namespace Api.Dtos;

public class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    /// <summary>Stable machine-readable error code (e.g. youTubePlaylistNotFound); omitted when not set.</summary>
    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; private set; }

    /// <summary>True when nothing was saved because a check could not be made and the same request can be retried.</summary>
    [JsonPropertyName("retryable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Retryable { get; private set; }

    public static ApiErrorResponse Failure(string message = "", string? code = null, bool? retryable = null)
    {
        return new ApiErrorResponse { Error = message, Code = code, Retryable = retryable };
    }
}
