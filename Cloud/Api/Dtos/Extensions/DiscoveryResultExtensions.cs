using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Models;

namespace Api.Dtos.Extensions;

public static class DiscoveryResultExtensions
{
    public static DiscoveryResponseItem ToDiscoveryResponseItem(this DiscoveryResult item,
        IDictionary<Guid, string> podcasts)
    {
        var result = new DiscoveryResponseItem();
        result.Urls = item.Urls;
        result.Released = item.Released;
        result.Description = item.Description;
        result.EnrichedTimeFromApple = item.EnrichedTimeFromApple;
        result.EnrichedUrlFromSpotify = item.EnrichedUrlFromSpotify;
        result.EpisodeName = item.EpisodeName;
        result.Id = item.Id;
        result.ImageUrl = item.ImageUrl;
        result.MatchingPodcasts = item.MatchingPodcastIds
            .Select(x =>
            {
                podcasts.TryGetValue(x, out var podcast);
                return new MatchingPodcast {Id = x, Name = podcast ?? string.Empty};
            })
            .ToArray();
        result.ShowName = item.ShowName;
        result.YouTubeChannelMembers = item.YouTubeChannelMembers;
        result.YouTubeViews = item.YouTubeViews;
        result.Sources = item.Sources
            .Select(x =>
                x.ConvertEnumByName<RedditPodcastPoster.Models.DiscoverService, DiscoverService>(true))
            .ToArray();
        return result;
    }
}