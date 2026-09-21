namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// Guards SubmitUrl <c>--episode-id</c> mode: requires explicit <c>-r</c> and rejects
/// url/file / <c>-f</c> / <c>-l</c> / <c>-c</c> combinations.
/// </summary>
public static class SubmitUrlEpisodeIdModeValidator
{
    public static void EnsureValid(
        bool refreshMeta,
        bool submitUrlsInFile,
        bool isInternetArchivePlaylist,
        bool createPodcast,
        string? urlOrFile)
    {
        if (!refreshMeta)
        {
            throw new InvalidOperationException(
                "--episode-id requires -r / --refresh-meta so overwrite of release/duration/title is explicit.");
        }

        if (submitUrlsInFile ||
            isInternetArchivePlaylist ||
            createPodcast ||
            !string.IsNullOrWhiteSpace(urlOrFile))
        {
            throw new InvalidOperationException(
                "--episode-id cannot be combined with a url/file, -f, -l, or -c.");
        }
    }
}
