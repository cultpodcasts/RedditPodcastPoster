using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Models.Catalogue;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// The indexer's write-back of a news organisation's per-channel YouTube flags on
/// <see cref="Models.News.NewsOrganisation.AdditionalYouTubeChannels"/> (#1370, <c>/additionalYouTubeChannels</c> on the
/// news organisation document). The list holds curator-owned channel and playlist ids beside the indexer-owned
/// <c>youTubeChannelSearchForbidden</c> / <c>youTubePlaylistQueryIsExpensive</c>, and can only be set as a whole, so it is
/// one single-operation patch guarded by <see cref="UnchangedFilter"/>: it only applies while the stored channels and
/// playlists are still the ones the pass read (same order). If a curator changed them mid-pass the precondition fails,
/// the flags are not written, and the next pass measures them again. News organisations only: a TV show has no
/// additional channels.
/// </summary>
public static class NewsOrganisationAdditionalYouTubeChannelsPatchOperations
{
    /// <summary>JSON name of <see cref="Models.News.NewsOrganisation.AdditionalYouTubeChannels"/>.</summary>
    public const string Member = "additionalYouTubeChannels";

    public const string Path = "/" + Member;

    /// <summary>The set of <c>/additionalYouTubeChannels</c>, serialised with the container's own options.</summary>
    public static PatchOperation<JsonNode?> Build(
        IReadOnlyList<AdditionalYouTubeChannel> channels,
        JsonSerializerOptions? serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(channels);
        var value = JsonSerializer.SerializeToNode(channels, serializerOptions) as JsonArray
                    ?? throw new InvalidOperationException("Additional YouTube channels did not serialise to a JSON array.");
        return (PatchOperation<JsonNode?>) PatchOperation.Set<JsonNode?>(Path, value);
    }

    /// <summary>
    /// Cosmos patch filter that matches only while the stored <c>additionalYouTubeChannels</c> holds exactly the channel
    /// ids and playlist ids of <paramref name="channels"/>, in order (the flags may differ: those are what the patch
    /// writes).
    /// </summary>
    public static string UnchangedFilter(JsonArray channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        const string list = "c[\"" + Member + "\"]";
        var conditions = new List<string> { $"IS_ARRAY({list})", $"ARRAY_LENGTH({list}) = {channels.Count}" };
        for (var i = 0; i < channels.Count; i++)
        {
            var channel = channels[i] as JsonObject;
            var channelId = StringMember(channel, "youTubeChannelId");
            var playlistId = StringMember(channel, "youTubePlaylistId");
            conditions.Add(channelId is null
                ? $"NOT IS_DEFINED({list}[{i}].youTubeChannelId)"
                : $"{list}[{i}].youTubeChannelId = '{Quote(channelId)}'");
            conditions.Add(playlistId is null
                ? $"(NOT IS_DEFINED({list}[{i}].youTubePlaylistId) OR IS_NULL({list}[{i}].youTubePlaylistId))"
                : $"{list}[{i}].youTubePlaylistId = '{Quote(playlistId)}'");
        }

        return "FROM c WHERE " + string.Join(" AND ", conditions);
    }

    /// <summary>
    /// Applies the guarded set. A failed precondition (a curator changed the channels mid-pass) is not an error: the
    /// flags are measured again next pass. Returns false when the organisation no longer exists.
    /// </summary>
    internal static async Task<bool> Apply(
        Container container,
        Guid id,
        IReadOnlyList<AdditionalYouTubeChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);
        if (channels.Count == 0)
        {
            return true;
        }

        var options = container.Database.Client.ClientOptions.UseSystemTextJsonSerializerWithOptions;
        var set = Build(channels, options);
        try
        {
            await container.PatchItemAsync<JsonObject>(
                id.ToString(), new PartitionKey(id.ToString()), [set],
                new PatchItemRequestOptions
                {
                    EnableContentResponseOnWrite = false,
                    FilterPredicate = UnchangedFilter((JsonArray) set.Value!)
                });
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            // The curator changed the channels during the pass; their edit wins.
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        return true;
    }

    private static string? StringMember(JsonObject? node, string name) =>
        node is not null && node.TryGetPropertyValue(name, out var value) && value is JsonValue jsonValue &&
        jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;

    private static string Quote(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
