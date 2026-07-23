using System.Text.Json.Serialization;

namespace Api.Dtos;

public class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; private set; }

    public static ApiErrorResponse Failure(string message = "")
    {
        return new ApiErrorResponse { Error = message };
    }
}