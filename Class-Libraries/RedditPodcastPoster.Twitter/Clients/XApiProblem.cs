using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// The JSON error body X returns for a failed v2 request: a problem object (<c>title</c>, <c>detail</c>,
/// <c>type</c>, <c>status</c>) and/or an <c>errors</c> array. See
/// https://docs.x.com/x-api/fundamentals/response-codes-and-errors.
/// </summary>
public sealed class XApiProblem
{
    public const string UsageCappedType = "https://api.twitter.com/2/problems/usage-capped";

    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("detail")] public string? Detail { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("status")] public int? Status { get; set; }
    [JsonPropertyName("errors")] public XApiError[]? Errors { get; set; }

    /// <summary>Parses an X error body; returns an empty problem for an empty or non-JSON body.</summary>
    public static XApiProblem Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new XApiProblem();
        }

        try
        {
            return JsonSerializer.Deserialize<XApiProblem>(body) ?? new XApiProblem();
        }
        catch (JsonException)
        {
            return new XApiProblem();
        }
    }

    /// <summary>X refuses a post whose text matches a recent post from the same account.</summary>
    [JsonIgnore]
    public bool IsDuplicateContent =>
        Messages().Any(m => m.Contains("duplicate content", StringComparison.OrdinalIgnoreCase)) ||
        (Errors?.Any(e => e.Code == 187) ?? false);

    /// <summary>A rate limit or the app's usage cap; nothing more should be posted this run.</summary>
    [JsonIgnore]
    public bool IsRateOrUsageLimited =>
        (Type?.EndsWith("/usage-capped", StringComparison.OrdinalIgnoreCase) ?? false) ||
        Messages().Any(m => m.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                            m.Contains("usage cap", StringComparison.OrdinalIgnoreCase));

    /// <summary>A one-line summary for logs (no request data).</summary>
    public string Summary() => string.Join(" | ", Messages().Prepend(Type ?? string.Empty).Where(m => m != string.Empty));

    private IEnumerable<string> Messages()
    {
        if (!string.IsNullOrWhiteSpace(Title)) yield return Title;
        if (!string.IsNullOrWhiteSpace(Detail)) yield return Detail;
        foreach (var error in Errors ?? [])
        {
            if (!string.IsNullOrWhiteSpace(error.Message)) yield return error.Message;
            if (!string.IsNullOrWhiteSpace(error.Detail)) yield return error.Detail;
        }
    }
}

public sealed class XApiError
{
    [JsonPropertyName("code")] public int? Code { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("detail")] public string? Detail { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
}
