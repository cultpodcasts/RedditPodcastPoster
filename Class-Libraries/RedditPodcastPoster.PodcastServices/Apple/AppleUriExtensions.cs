namespace RedditPodcastPoster.PodcastServices.Apple;

public static class AppleUriExtensions
{
    public static Uri CleanAppleUrl(this Uri appleUrl)
    {
        const string prefix = "/podcast";
        const string suffix = "&uo=4";
        if (appleUrl.PathAndQuery.StartsWith(prefix) && !appleUrl.PathAndQuery.EndsWith(suffix))
        {
            return appleUrl;
        }

        var applePath = appleUrl.PathAndQuery;
        if (!appleUrl.PathAndQuery.StartsWith(prefix))
        {
            var podcastsIndex = appleUrl.PathAndQuery.IndexOf(prefix, StringComparison.Ordinal);
            applePath = appleUrl.PathAndQuery.Substring(podcastsIndex);
        }

        if (applePath.EndsWith(suffix))
        {
            applePath =
                applePath.Substring(0, applePath.Length - suffix.Length);
        }

        return new Uri($"{appleUrl.Scheme}://{appleUrl.Host}{applePath}", UriKind.Absolute);
    }
}