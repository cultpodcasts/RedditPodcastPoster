using System.Text.RegularExpressions;
using RedditPodcastPoster.Subreddit;

namespace TextClassifierTraining;

public static class RedditPostExtensions
{
    private static readonly Regex AppleUrl = new(@"\?i=(?'episodeid'\d+)", RegexOptions.Compiled);
    private static readonly Regex SpotifyUrl = new("episode/(?'episodeid'[a-zA-Z0-9]+)/?", RegexOptions.Compiled);
    private static readonly Regex YouTubeShortUrl = new(@"/(?'episodeid'\w+)", RegexOptions.Compiled);
    private static readonly Regex YouTubeUrl = new(@"v=(?'episodeid'\w+)", RegexOptions.Compiled);

    public static RedditPostMetaData? ToRedditPostMetaData(this RedditPost redditPost)
    {
        long? appleId = null;
        var spotifyId = string.Empty;
        var youTubeId = string.Empty;

        if (!string.IsNullOrWhiteSpace(redditPost.Url) && redditPost.Url.StartsWith("http"))
        {
            var url = new Uri(redditPost.Url, UriKind.Absolute);
            if (url.Host.Contains("apple"))
            {
                var match = AppleUrl.Match(redditPost.Url);
                if (match.Success)
                {
                    var value = match.Groups["episodeid"].Value;
                    appleId = long.Parse(value);
                }
            }
            else if (url.Host.Contains("spotify"))
            {
                var match = SpotifyUrl.Match(redditPost.Url);
                if (match.Success)
                {
                    spotifyId = match.Groups["episodeid"].Value;
                }
            }
            else if (url.Host.Contains("youtube") || url.Host.Contains("youtu.be"))
            {
                var match = YouTubeUrl.Match(redditPost.Url);
                if (match.Success)
                {
                    youTubeId = match.Groups["episodeid"].Value;
                }
                else
                {
                    match = YouTubeShortUrl.Match(redditPost.Url);
                    if (match.Success)
                    {
                        youTubeId = match.Groups["episodeid"].Value;
                    }
                }
            }

            return new RedditPostMetaData
            {
                Flair = redditPost.LinkFlairText,
                AppleId = appleId,
                SpotifyId = spotifyId,
                YouTubeId = youTubeId
            };
        }

        return null;
    }
}