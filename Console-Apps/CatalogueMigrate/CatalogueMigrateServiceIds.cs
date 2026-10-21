using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using RedditPodcastPoster.Models.Podcasts;

namespace CatalogueMigrate;

/// <summary>
/// Which of the podcast's service ids (YouTube channel / playlist, Spotify, Apple) the destination parent
/// document would not carry. Checked on the parent's own JSON, anywhere in the document (e.g. under
/// <c>indexing</c>), so the guard holds whatever shape the mapper writes. A dropped channel id is how a
/// moved news organisation stops matching its submits (the NBC News failure).
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
