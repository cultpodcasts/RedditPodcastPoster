using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>Where an episode URL belongs: its owner ids, and the podcast or news organisation / TV show that gets it.</summary>
/// <param name="Owner">The owner lookup; null when it was not run.</param>
/// <param name="Podcast">The podcast that keeps the URL (may be removed: callers check).</param>
/// <param name="Parent">The news organisation / TV show that gets the URL instead of a podcast.</param>
public sealed record EpisodeUrlRoute(EpisodeUrlOwner? Owner, Podcast? Podcast, CatalogueParentMatch? Parent);

/// <summary>
/// The single channel-owner routing rule for a URL with no curator target, shared by the toolbar submit
/// (<see cref="Submitters.UrlSubmitter"/>) and discovery (<see cref="Submitters.DiscoveryUrlSubmitter"/>): a URL
/// whose channel / show a news organisation or TV show owns is never filed under (or as) a new podcast.
/// </summary>
public interface IEpisodeUrlRouter
{
    /// <summary>
    /// Owner lookup, then: the owning podcast keeps the URL, unless it is playlist-scoped on a channel a news
    /// organisation / TV show owns and the video is not in its playlist; with no owning podcast, the news
    /// organisation / TV show that owns the channel / show gets it. Non-platform URLs only get the podcast.
    /// Owner-lookup failures propagate (the caller cannot tell who owns the URL).
    /// </summary>
    Task<EpisodeUrlRoute> RouteByOwner(Uri url, IndexingContext indexingContext);

    /// <summary>
    /// A podcast matched only by YouTube channel, scoped to one playlist of that channel (e.g. a show on a news
    /// outlet's channel), keeps the URL only when the video is in its playlist. Otherwise the news organisation
    /// or TV show that owns the channel gets it. With no owning parent the podcast keeps it (unchanged).
    /// </summary>
    Task<(Podcast? Podcast, CatalogueParentMatch? Parent)> PreferParentOverPlaylistScopedPodcast(
        Podcast podcast,
        EpisodeUrlOwnerIds ids,
        Uri url,
        IndexingContext indexingContext);

    /// <summary>
    /// The news organisation / TV show that owns the URL's channel / show, resolving the owner ids when
    /// <paramref name="owner"/> is null. A lookup failure fails closed (<see cref="InvalidOperationException"/>):
    /// with the owner unknown, going on would create a podcast for a channel a parent may own.
    /// </summary>
    Task<CatalogueParentMatch?> FindOwningParent(Uri url, EpisodeUrlOwner? owner, IndexingContext indexingContext);
}

public class EpisodeUrlRouter(
    IPodcastService podcastService,
    ICatalogueParentLookup catalogueParentLookup,
    ILogger<EpisodeUrlRouter> logger) : IEpisodeUrlRouter
{
    /// <summary>Spotify, YouTube or Apple Podcasts: the URLs that can be routed to a news organisation or TV show.</summary>
    public static bool IsPodcastPlatformUrl(Uri url) =>
        SpotifyPodcastServiceMatcher.IsMatch(url) ||
        YouTubePodcastServiceMatcher.IsMatch(url) ||
        ApplePodcastServiceMatcher.IsMatch(url);

    public async Task<EpisodeUrlRoute> RouteByOwner(Uri url, IndexingContext indexingContext)
    {
        var owner = await podcastService.GetEpisodeUrlOwner(url, indexingContext);
        var podcast = owner?.Podcast;
        if (owner == null || !IsPodcastPlatformUrl(url))
        {
            return new EpisodeUrlRoute(owner, podcast, null);
        }

        if (podcast != null)
        {
            var (kept, parent) =
                await PreferParentOverPlaylistScopedPodcast(podcast, owner.Ids, url, indexingContext);
            return new EpisodeUrlRoute(owner, kept, parent);
        }

        return new EpisodeUrlRoute(owner, null, await FindOwningParent(url, owner, indexingContext));
    }

    public async Task<(Podcast? Podcast, CatalogueParentMatch? Parent)> PreferParentOverPlaylistScopedPodcast(
        Podcast podcast,
        EpisodeUrlOwnerIds ids,
        Uri url,
        IndexingContext indexingContext)
    {
        if (string.IsNullOrWhiteSpace(ids.YouTubeChannelId) ||
            string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId) ||
            podcast.YouTubeChannelId != ids.YouTubeChannelId)
        {
            return (podcast, null);
        }

        var channelOwner = await catalogueParentLookup.FindByOwner(ids);
        if (channelOwner == null)
        {
            return (podcast, null);
        }

        var inPlaylist = await podcastService.IsVideoInPodcastPlaylist(podcast, url, indexingContext);
        if (inPlaylist == true)
        {
            return (podcast, null);
        }

        if (inPlaylist == null)
        {
            throw new InvalidOperationException(
                $"Could not check whether '{url}' is in the playlist of podcast '{podcast.Name}', which shares its channel with {channelOwner.Kind} '{channelOwner.Parent.Name}'. Retry, or submit with the podcast or {channelOwner.Kind} name.");
        }

        logger.LogInformation(
            "Video '{Url}' is not in playlist '{PlaylistId}' of podcast '{PodcastName}' ({PodcastId}); routing to channel owner {Kind} '{ParentName}' ({ParentId}).",
            url, podcast.YouTubePlaylistId, podcast.Name, podcast.Id, channelOwner.Kind, channelOwner.Parent.Name,
            channelOwner.Parent.Id);
        return (null, channelOwner);
    }

    public async Task<CatalogueParentMatch?> FindOwningParent(
        Uri url,
        EpisodeUrlOwner? owner,
        IndexingContext indexingContext)
    {
        owner ??= await TryGetEpisodeUrlOwner(url, indexingContext);
        return owner == null ? null : await catalogueParentLookup.FindByOwner(owner.Ids);
    }

    private async Task<EpisodeUrlOwner?> TryGetEpisodeUrlOwner(Uri url, IndexingContext indexingContext)
    {
        try
        {
            return await podcastService.GetEpisodeUrlOwner(url, indexingContext);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            logger.LogError(e,
                "Could not resolve the channel / show of '{Url}'; refusing to create a podcast it might not own.", url);
            throw new InvalidOperationException(
                $"Could not check which podcast, news organisation or TV show owns '{url}'. Retry, or submit with a podcast, news organisation or TV show.",
                e);
        }
    }
}
