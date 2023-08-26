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
        return item?.Url;
    }
}