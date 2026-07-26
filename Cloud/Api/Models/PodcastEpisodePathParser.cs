namespace Api.Models;

/// <summary>
/// Splits route paths where the podcast name may contain '/' (after %2F decode)
/// and the final segment is an episode id.
/// </summary>
public static class PodcastEpisodePathParser
{
    public static bool TrySplitTrailingEpisodeId(
        string path,
        out string podcastIdentifier,
        out Guid episodeId)
    {
        podcastIdentifier = string.Empty;
        episodeId = default;

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return false;
        }

        if (!Guid.TryParse(path.AsSpan(lastSlash + 1), out episodeId))
        {
            return false;
        }

        podcastIdentifier = path[..lastSlash];
        return podcastIdentifier.Length > 0;
    }
}
