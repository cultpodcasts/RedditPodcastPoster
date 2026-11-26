using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace Api.Dtos.Mapping;

/// <summary>Maps service/platform domain models to their API DTOs, preserving null (except null entries in a services map, which are dropped).</summary>
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
        service?.ToDto();

    /// <summary>Maps by name; throws on an unmapped member so enum drift fails loudly.</summary>
    public static ServiceDto ToDto(this Service service) =>
        service switch
        {
            Service.Spotify => ServiceDto.Spotify,
            Service.Apple => ServiceDto.Apple,
            Service.YouTube => ServiceDto.YouTube,
            Service.Other => ServiceDto.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "Unmapped Service value.")
        };

    public static EpisodeImagesDto? ToDto(this EpisodeImages? images) =>
        images is null
            ? null
            : new() { YouTube = images.YouTube, Spotify = images.Spotify, Apple = images.Apple, Other = images.Other };

    public static ServiceLinkDto ToDto(this ServiceLink link) =>
        new() { Url = link.Url, Image = link.Image, Language = link.Language };

    /// <summary>
    /// Maps a stored services map. A null entry (stored <c>"key": null</c>) is not a usable link and is
    /// left out, matching the contract's <c>Record&lt;string, ServiceLinkDto&gt;</c> (values never null)
    /// instead of throwing (#1508).
    /// </summary>
    public static Dictionary<string, ServiceLinkDto>? ToDtos(this IDictionary<string, ServiceLink>? services) =>
        services?
            .Where(kv => kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToDto());

    public static DiscoveryResultUrlsDto ToDto(this DiscoveryResultUrls urls) =>
        new() { Spotify = urls.Spotify, Apple = urls.Apple, YouTube = urls.YouTube };
}
