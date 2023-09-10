using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleUrlResolver : IAppleUrlResolver
{
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly IApplePodcastEnricher _applePodcastEnricher;
    private readonly ILogger<AppleUrlResolver> _logger;

    public AppleUrlResolver(
        IAppleEpisodeResolver appleEpisodeResolver,
        IApplePodcastEnricher applePodcastEnricher,
        ILogger<AppleUrlResolver> logger)
    {
        _appleEpisodeResolver = appleEpisodeResolver;
        _applePodcastEnricher = applePodcastEnricher;
        _logger = logger;
    }

    public async Task<Uri?> Resolve(Podcast podcast, Episode episode)
    {
        if (podcast.AppleId == null)
        {
            await _applePodcastEnricher.AddId(podcast);
        }

        var item = await _appleEpisodeResolver.FindEpisode(FindAppleEpisodeRequestFactory.Create(podcast, episode));
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

        if (applePath.EndsWith(suffix))
        {
            applePath =
                applePath.Substring(0, applePath.Length - suffix.Length);
        }

        return new Uri($"{appleUrl.Scheme}://{appleUrl.Host}{applePath}", UriKind.Absolute);
    }
}