using System.Text.RegularExpressions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public static partial class YouTubePlaylistIdResolver
{
    private static readonly Regex PlaylistIdRegex = CreatePlaylistIdRegex();

    public static string? Extract(Uri youTubeUrl)
    {
        var playlistIdMatch = PlaylistIdRegex.Match(youTubeUrl.ToString()).Groups["playlistId"];
        return playlistIdMatch.Success ? playlistIdMatch.Value : null;
    }

    [GeneratedRegex(@"&list=(?'playlistId'PL[\w-_]*)")]
    private static partial Regex CreatePlaylistIdRegex();
}