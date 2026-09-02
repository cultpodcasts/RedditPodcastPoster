namespace RedditPodcastPoster.Vimeo.Matching;

public static class VimeoUrlMatcher
{
    public static bool IsSubmitUrl(Uri url)
    {
        var host = url.Host.ToLowerInvariant();
        if (!host.Contains("vimeo.com"))
        {
            return false;
        }

        return url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Length > 0 && part.All(char.IsDigit));
    }
}
