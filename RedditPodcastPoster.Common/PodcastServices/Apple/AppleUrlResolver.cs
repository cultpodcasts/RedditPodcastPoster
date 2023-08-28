using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleUrlResolver : IAppleUrlResolver
{
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly ILogger<AppleUrlResolver> _logger;

    public AppleUrlResolver(IAppleEpisodeResolver appleEpisodeResolver, iTunesSearchManager iTunesSearchManager,
        ILogger<AppleUrlResolver> logger)
    {
        _appleEpisodeResolver = appleEpisodeResolver;
        _logger = logger;
    }

    public async Task<Uri?> Resolve(Podcast podcast, Episode episode)
    {
        var item = await _appleEpisodeResolver.FindEpisode(podcast, episode);
        if (item == null)
        {
            return null;
        }

        return CleanUrl(item.Url);
    }

    public static Uri CleanUrl(Uri appleUrl)
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
            var podcastsIndex = appleUrl.PathAndQuery.IndexOf(prefix);
            applePath = appleUrl.PathAndQuery.Substring(podcastsIndex);
        }
        else
        {
            var breakpoint = 1;
        }

        if (applePath.EndsWith(suffix))
        {
            applePath =
                applePath.Substring(0, applePath.Length - suffix.Length);
        }
        else
        {
            var breakpoint = 1;
        }

        return new Uri($"{appleUrl.Scheme}://{appleUrl.Host}{applePath}", UriKind.Absolute);
    }
}