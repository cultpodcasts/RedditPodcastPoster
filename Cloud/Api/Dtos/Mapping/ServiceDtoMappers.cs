using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace Api.Dtos.Mapping;

/// <summary>Maps service/platform domain models to their API DTOs, preserving null.</summary>
public static class ServiceDtoMappers
{
    public static EpisodeIdsDto? ToDto(this EpisodeIds? ids) =>
        ids is null ? null : new() { Spotify = ids.Spotify, Apple = ids.Apple, YouTube = ids.YouTube };

    public static ServiceUrlsDto ToDto(this ServiceUrls urls) =>
        new()
        {
            Spotify = urls.Spotify,
            Apple = urls.Apple,
            YouTube = urls.YouTube,
            InternetArchive = urls.InternetArchive,
            BBC = urls.BBC
        };

    public static ServiceDto? ToDto(this Service? service) =>
        service is null ? null : (ServiceDto)(int)service.Value;

    public static EpisodeImagesDto? ToDto(this EpisodeImages? images) =>
        images is null
            ? null
            : new() { YouTube = images.YouTube, Spotify = images.Spotify, Apple = images.Apple, Other = images.Other };

    public static ServiceLinkDto ToDto(this ServiceLink link) =>
        new() { Url = link.Url, Image = link.Image, Language = link.Language };

    public static Dictionary<string, ServiceLinkDto>? ToDtos(this IDictionary<string, ServiceLink>? services) =>
        services?.ToDictionary(kv => kv.Key, kv => kv.Value.ToDto());

    public static DiscoveryResultUrlsDto ToDto(this DiscoveryResultUrls urls) =>
        new() { Spotify = urls.Spotify, Apple = urls.Apple, YouTube = urls.YouTube };
}
