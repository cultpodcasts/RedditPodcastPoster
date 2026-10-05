using System.Text.Json.Serialization;

namespace RedditPodcastPoster.TheTvdb.Clients;

internal static class TheTvdbJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed class TheTvdbLoginRequest
{
    [JsonPropertyName("apikey")]
    public required string ApiKey { get; init; }

    [JsonPropertyName("pin")]
    public string? Pin { get; init; }
}

internal sealed class TheTvdbEnvelope<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

internal sealed class TheTvdbLoginData
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }
}

internal sealed class TheTvdbRemoteId
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("sourceName")]
    public string? SourceName { get; init; }
}

internal sealed class TheTvdbSearchHitDto
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("tvdb_id")]
    public string? TvdbId { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("network")]
    public string? Network { get; init; }

    [JsonPropertyName("remote_ids")]
    public List<TheTvdbRemoteId>? RemoteIds { get; init; }
}

internal sealed class TheTvdbSeriesDto
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("originalCountry")]
    public string? OriginalCountry { get; init; }

    [JsonPropertyName("remoteIds")]
    public List<TheTvdbRemoteId>? RemoteIds { get; init; }
}

internal sealed class TheTvdbEpisodeDto
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("seriesId")]
    public long SeriesId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("number")]
    public int? Number { get; init; }

    [JsonPropertyName("seasonNumber")]
    public int? SeasonNumber { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("remoteIds")]
    public List<TheTvdbRemoteId>? RemoteIds { get; init; }
}

internal sealed class TheTvdbEpisodePageDto
{
    [JsonPropertyName("series")]
    public TheTvdbSeriesDto? Series { get; init; }

    [JsonPropertyName("episodes")]
    public List<TheTvdbEpisodeDto>? Episodes { get; init; }
}
