using Api.Models;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Dtos.Extensions;

public static class DiscoveryResultExtensions
{
    public static DiscoveryResponse ToDto(this DiscoveryCurationData data)
    {
        var podcastsLookup = data.Podcasts.ToDictionary(
            kv => kv.Key,
            kv => new DiscoveryPodcast
            {
                Name = kv.Value.Name,
                IsVisible = kv.Value.IsVisible,
                VisibleEpisodes = kv.Value.VisibleEpisodes
            });

        return new DiscoveryResponse
        {
            Ids = data.Ids,
            Results = data.Results
                .Select(x => x.ToDiscoveryResponseItem(podcastsLookup))
                .OrderByDescending(x => x.AcceptProbability ?? -1f)
                .ThenBy(x => x.Released),
            HiddenCount = data.HiddenCount
        };
    }

    public static DiscoveryResponseItem ToDiscoveryResponseItem(
        this DiscoveryResult item,
        IDictionary<Guid, DiscoveryPodcast> podcasts)
    {
        var result = new DiscoveryResponseItem();
        result.Urls = item.Urls;
        result.Released = item.Released;
        result.Description = item.Description;
        result.ShowDescription = item.ShowDescription;
        result.EnrichedTimeFromApple = item.EnrichedTimeFromApple;
        result.EnrichedUrlFromSpotify = item.EnrichedUrlFromSpotify;
        result.EpisodeName = item.EpisodeName;
        result.Id = item.Id;
        result.ImageUrl = item.ImageUrl;
        result.Length = item.Length;
        var resultMatchingPodcasts = item.MatchingPodcastIds
            .Select(x =>
            {
                podcasts.TryGetValue(x, out var podcast);
                return podcast;
            })
            .Where(x => x != null)
            .Distinct()
            .ToArray();
        result.MatchingPodcasts = (resultMatchingPodcasts.Any() ? resultMatchingPodcasts : null)!;
        result.ShowName = item.ShowName;
        result.YouTubeChannelMembers = item.YouTubeChannelMembers;
        result.YouTubeViews = item.YouTubeViews;
        result.Subjects = item.Subjects;
        result.Sources = item.Sources
            .Select(x =>
                x.ConvertEnumByName<RedditPodcastPoster.Models.Discovery.DiscoverService, DiscoverService>(true))
            .ToArray();
        result.AcceptProbability = item.AcceptProbability;
        result.AutoHidden = item.AutoHidden;
        return result;
    }
}
