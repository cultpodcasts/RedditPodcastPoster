using System.Net;
using System.Text.Json;
using RedditPodcastPoster.Cloudflare.Models;

namespace RedditPodcastPoster.Cloudflare.Clients;

/// <summary>
/// Parses Cloudflare KV metadata GET responses into <see cref="KVRecord"/>.
/// The metadata endpoint returns <c>{ success, result }</c>, not a write-shaped record with <c>key</c>/<c>value</c>.
/// </summary>
public static class KVMetadataResponseParser
{
    public static KVRecord? Parse(
        HttpStatusCode statusCode,
        string json,
        string key,
        JsonSerializerOptions jsonSerializerOptions)
    {
        if (statusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (statusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        CloudflareApiResponse<JsonElement>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CloudflareApiResponse<JsonElement>>(json, jsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (envelope is not { Success: true })
        {
            return null;
        }

        MetaData? metadata = null;
        if (envelope.Result.ValueKind is JsonValueKind.Object)
        {
            try
            {
                metadata = envelope.Result.Deserialize<MetaData>(jsonSerializerOptions);
            }
            catch (JsonException)
            {
                // Key exists but metadata is empty/partial — still treat as present.
            }
        }

        return new KVRecord
        {
            Key = key,
            Metadata = metadata,
            Value = ""
        };
    }
}
