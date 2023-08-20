using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleUrlResolver : IAppleUrlResolver
{
    private readonly IAppleItemResolver _appleItemResolver;
    private readonly ILogger<AppleUrlResolver> _logger;

    public AppleUrlResolver(IAppleItemResolver appleItemResolver, iTunesSearchManager iTunesSearchManager,
        ILogger<AppleUrlResolver> logger)
    {
        _appleItemResolver = appleItemResolver;
        _logger = logger;
    }

    public async Task<Uri?> Resolve(Podcast podcast, Episode episode)
    {
        var item = await _appleItemResolver.FindEpisode(podcast, episode);
        return item?.Url;
    }
}