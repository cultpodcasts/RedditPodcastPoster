using System.Text.RegularExpressions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Shape of a YouTube playlist id: letters, digits, '-' and '_', 13-64 characters (PL..., UU..., OL..., and older
/// short ids). Lets a pasted URL or a stray character be refused before spending quota on playlists.list.
/// </summary>
public static partial class YouTubePlaylistIdFormat
{
    public static bool IsValid(string? playlistId) =>
        playlistId != null && PlaylistIdRegex().IsMatch(playlistId);

    /// <summary>The list= value of a pasted YouTube URL, when it is a valid id; otherwise null.</summary>
    public static string? TryExtractFromUrl(string? value)
    {
        if (value == null)
        {
            return null;
        }

        var match = ListParameterRegex().Match(value);
        return match.Success && IsValid(match.Groups["id"].Value) ? match.Groups["id"].Value : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{13,64}$")]
    private static partial Regex PlaylistIdRegex();

    [GeneratedRegex("[?&]list=(?<id>[^&#]+)")]
    private static partial Regex ListParameterRegex();
}
