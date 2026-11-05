using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace CatalogueMigrate;

/// <summary>
/// Which of the podcast's service ids (YouTube channel / playlist, Spotify, Apple) the destination parent
/// document would not carry. Checked on the parent's own JSON, anywhere in the document (e.g. under
/// <c>indexing</c>), so the guard holds whatever shape the mapper writes. A dropped channel id is how a
/// moved news organisation stops matching its submits (the NBC News failure).
/// <para>
/// Also validates that a copied <c>youTubeChannelId</c> / <c>youTubePlaylistId</c> pair is owned correctly
/// (#1178) before dry-run reports apply-eligible or apply writes the parent.
/// </para>
/// </summary>
public static class CatalogueMigrateServiceIds
{
    public static IReadOnlyList<string> Dropped(Podcast source, object destinationParent)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destinationParent);
        (string Name, string? Value)[] expected =
        [
            ("youTubeChannelId", source.YouTubeChannelId),
            ("youTubePlaylistId", source.YouTubePlaylistId),
            ("spotifyId", source.SpotifyId),
            ("appleId", source.AppleId?.ToString(CultureInfo.InvariantCulture))
        ];

        var carried = new HashSet<(string, string)>();
        Collect(JsonSerializer.SerializeToNode(destinationParent, destinationParent.GetType()), carried);
        return expected
            .Where(id => !string.IsNullOrWhiteSpace(id.Value) && !carried.Contains((id.Name, id.Value!)))
            .Select(id => id.Name)
            .ToList();
    }

    /// <summary>
    /// Null when the podcast's YouTube channel/playlist pair may be copied. Otherwise the refused validation
    /// (1 quota unit when both ids are set). Channel-only or playlist-only parents are not checked.
    /// </summary>
    public static Task<YouTubePlaylistValidation?> YouTubePlaylistOwnershipRefusal(
        Podcast source,
        IYouTubePlaylistValidator validator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return YouTubePlaylistChannelOwnership.Check(
            validator,
            source.YouTubeChannelId,
            source.YouTubePlaylistId,
            cancellationToken);
    }

    /// <summary>Apply-block text for a refused YouTube playlist ownership check (dry-run and apply share it).</summary>
    public static string OwnershipBlockReason(YouTubePlaylistValidation refused)
    {
        ArgumentNullException.ThrowIfNull(refused);
        var code = refused.Code ?? "youTubePlaylistOwnershipRefused";
        return
            $"YouTube playlist ownership refused ({code}): {refused.Message} " +
            "Fix the podcast's youTubeChannelId / youTubePlaylistId pair before migrate, or clear one of the ids.";
    }

    private static void Collect(JsonNode? node, HashSet<(string, string)> carried)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    if (value is JsonValue scalar)
                    {
                        carried.Add((name, scalar.ToString()));
                    }
                    else
                    {
                        Collect(value, carried);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, carried);
                }

                break;
        }
    }
}
