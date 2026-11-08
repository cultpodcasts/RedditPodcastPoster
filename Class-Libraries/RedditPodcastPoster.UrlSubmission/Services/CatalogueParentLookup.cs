using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface ICatalogueParentLookup
{
    /// <summary>The news organisation or TV show with this id (a curator targeting a parent by id).</summary>
    Task<CatalogueParentMatch?> FindById(Guid id);

    /// <summary>The <paramref name="kind"/> row with this id only (an explicit parent target).</summary>
    Task<CatalogueParentMatch?> FindById(CatalogueParentKind kind, Guid id);

    /// <summary>Case-insensitive name match across news organisations and TV shows. Many → <see cref="AmbiguousParentNameException"/>.</summary>
    Task<CatalogueParentMatch?> FindByName(string name);

    /// <summary>
    /// The parent whose <c>indexing</c> holds the URL's channel / show id. Many → after the podcast tie-breaks
    /// (no playlist, then the single auto-indexed one) → <see cref="AmbiguousParentNameException"/>.
    /// Without a video URL, a channel-wide claim wins over playlist-scoped peers (same as podcasts).
    /// </summary>
    Task<CatalogueParentMatch?> FindByOwner(EpisodeUrlOwnerIds ids);

    /// <summary>
    /// Like <see cref="FindByOwner(EpisodeUrlOwnerIds)"/> for a YouTube video: a playlist-scoped parent whose
    /// playlist contains the video wins over a channel-wide peer on the same channel. Membership that cannot be
    /// checked fails closed. Spotify / Apple owner ids ignore <paramref name="url"/>.
    /// </summary>
    Task<CatalogueParentMatch?> FindByOwner(EpisodeUrlOwnerIds ids, Uri url, IndexingContext indexingContext);

    /// <summary>
    /// Fallback when no parent's <c>indexing</c> holds a YouTube channel id: the news organisation or TV show whose
    /// name is the channel title (case-insensitive, exact) and which claims no YouTube channel yet (no
    /// <c>indexing</c>, or an empty <c>youTubeChannelId</c>). Parents already claiming a channel, and removed parents
    /// (a name is too weak to block a submit on a removed parent), are skipped.
    /// Many → <see cref="AmbiguousParentNameException"/>.
    /// </summary>
    Task<CatalogueParentMatch?> FindUnclaimedByYouTubeChannelTitle(string channelTitle);
}

/// <summary>
/// Finds the <see cref="CatalogueParentMatch"/> a URL should go to instead of a podcast. Ids come from
/// <see cref="CatalogueIndexingSource"/> (RPP #1050), set by kind transfer or the curator PATCH.
/// </summary>
public class CatalogueParentLookup(
    INewsOrganisationRepository newsOrganisations,
    ITvShowRepository tvShows,
    IYouTubeServiceWrapper youTubeService,
    IYouTubePlaylistMembership youTubePlaylistMembership,
    ILogger<CatalogueParentLookup> logger) : ICatalogueParentLookup
{
    private const string MixedKind = "CatalogueParent";

    public async Task<CatalogueParentMatch?> FindById(Guid id)
    {
        var organisation = await newsOrganisations.GetNewsOrganisation(id);
        if (organisation != null)
        {
            return CatalogueParentMatch.For(organisation);
        }

        var show = await tvShows.GetTvShow(id);
        return show == null ? null : CatalogueParentMatch.For(show);
    }

    public async Task<CatalogueParentMatch?> FindById(CatalogueParentKind kind, Guid id)
    {
        if (kind == CatalogueParentKind.NewsOrganisation)
        {
            var organisation = await newsOrganisations.GetNewsOrganisation(id);
            return organisation == null ? null : CatalogueParentMatch.For(organisation);
        }

        var show = await tvShows.GetTvShow(id);
        return show == null ? null : CatalogueParentMatch.For(show);
    }

    public async Task<CatalogueParentMatch?> FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var organisations = await PublisherNameAttachLookup.FindByName(newsOrganisations, name);
        var shows = await PublisherNameAttachLookup.FindByName(tvShows, name);
        var matches = organisations.Select(CatalogueParentMatch.For).Concat(shows.Select(CatalogueParentMatch.For))
            .ToList();
        return Single(matches, name.Trim());
    }

    public Task<CatalogueParentMatch?> FindByOwner(EpisodeUrlOwnerIds ids) =>
        FindByOwnerCore(ids, url: null, indexingContext: null);

    public Task<CatalogueParentMatch?> FindByOwner(
        EpisodeUrlOwnerIds ids,
        Uri url,
        IndexingContext indexingContext) =>
        FindByOwnerCore(ids, url, indexingContext);

    private async Task<CatalogueParentMatch?> FindByOwnerCore(
        EpisodeUrlOwnerIds ids,
        Uri? url,
        IndexingContext? indexingContext)
    {
        if (ids.IsEmpty)
        {
            return null;
        }

        var matches = new List<CatalogueParentMatch>();
        if (!string.IsNullOrWhiteSpace(ids.YouTubeChannelId))
        {
            var channelId = ids.YouTubeChannelId;
            matches.AddRange((await newsOrganisations
                    .GetAllBy(x => x.Indexing != null && x.Indexing.YouTubeChannelId == channelId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            matches.AddRange((await tvShows
                    .GetAllBy(x => x.Indexing != null && x.Indexing.YouTubeChannelId == channelId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            if (matches.Count > 1)
            {
                matches = await ResolveYouTubeChannelTie(matches, channelId, url, indexingContext);
            }

            return Single(matches, channelId);
        }

        if (!string.IsNullOrWhiteSpace(ids.SpotifyShowId))
        {
            var spotifyId = ids.SpotifyShowId;
            matches.AddRange((await newsOrganisations
                    .GetAllBy(x => x.Indexing != null && x.Indexing.SpotifyId == spotifyId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            matches.AddRange((await tvShows
                    .GetAllBy(x => x.Indexing != null && x.Indexing.SpotifyId == spotifyId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            return Single(matches, spotifyId);
        }

        var appleId = ids.ApplePodcastId!.Value;
        matches.AddRange((await newsOrganisations
                .GetAllBy(x => x.Indexing != null && x.Indexing.AppleId == appleId).ToListAsync())
            .Select(CatalogueParentMatch.For));
        matches.AddRange((await tvShows
                .GetAllBy(x => x.Indexing != null && x.Indexing.AppleId == appleId).ToListAsync())
            .Select(CatalogueParentMatch.For));
        return Single(matches, appleId.ToString());
    }

    /// <summary>
    /// With a video URL: playlist-scoped parents that contain the video win (same idea as
    /// <c>PodcastService.MatchYouTubeChannelOwner</c>). Without a URL: channel-wide peers win over playlist-scoped.
    /// </summary>
    private async Task<List<CatalogueParentMatch>> ResolveYouTubeChannelTie(
        List<CatalogueParentMatch> matches,
        string channelId,
        Uri? url,
        IndexingContext? indexingContext)
    {
        var channelWide = matches.Where(x => string.IsNullOrWhiteSpace(x.Indexing?.YouTubePlaylistId)).ToList();
        var playlistScoped = matches.Where(x => !string.IsNullOrWhiteSpace(x.Indexing?.YouTubePlaylistId)).ToList();

        if (url is not null && indexingContext is not null && playlistScoped.Count > 0)
        {
            var containing = new List<CatalogueParentMatch>();
            var unknown = new List<CatalogueParentMatch>();
            foreach (var candidate in playlistScoped)
            {
                var inPlaylist = await IsVideoInPlaylist(candidate.Indexing!.YouTubePlaylistId, url, indexingContext);
                if (inPlaylist == true)
                {
                    containing.Add(candidate);
                }
                else if (inPlaylist is null)
                {
                    unknown.Add(candidate);
                }
            }

            if (containing.Count == 1)
            {
                return containing;
            }

            if (containing.Count > 1)
            {
                return ApplyAutoIndexTieBreak(containing);
            }

            if (unknown.Count > 0)
            {
                var ids = string.Join(", ", unknown.Select(x => $"'{x.Parent.Id}'"));
                var message =
                    $"Could not check whether '{url}' is in the playlist of catalogue parent(s) {ids} on youtube-channel-id '{channelId}'. Retry, or submit with a news organisation or TV show name.";
                logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            // Every playlist-scoped candidate is a confirmed miss — never return them (auto-index must not
            // pick a playlist that does not contain the video). Channel-wide peers only; empty → null.
            return ApplyAutoIndexTieBreak(channelWide);
        }

        if (channelWide.Count > 0)
        {
            matches = channelWide;
        }

        return ApplyAutoIndexTieBreak(matches);
    }

    private static List<CatalogueParentMatch> ApplyAutoIndexTieBreak(List<CatalogueParentMatch> matches)
    {
        if (matches.Count > 1 && matches.Count(x => x.Indexing?.IsAutoIndexed == true) == 1)
        {
            return matches.Where(x => x.Indexing?.IsAutoIndexed == true).ToList();
        }

        return matches;
    }

    /// <summary>
    /// True when <paramref name="playlistId"/> is blank (channel-wide), the URL's <c>list=</c> matches, or YouTube
    /// lists the video in it. False when not in it or the stored id is malformed; null only when it could not be
    /// checked (quota, outage).
    /// </summary>
    private async Task<bool?> IsVideoInPlaylist(string? playlistId, Uri url, IndexingContext indexingContext)
    {
        if (string.IsNullOrWhiteSpace(playlistId))
        {
            return true;
        }

        if (string.Equals(YouTubePlaylistIdResolver.Extract(url), playlistId, StringComparison.Ordinal))
        {
            return true;
        }

        if (!YouTubePlaylistIdFormat.IsValid(playlistId))
        {
            logger.LogWarning(
                "Stored youtube-playlist-id '{PlaylistId}' is malformed; treating '{Url}' as not in it. Fix the stored id.",
                playlistId, url);
            return false;
        }

        var videoId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(videoId) || indexingContext.SkipYouTubeUrlResolving)
        {
            return null;
        }

        return await youTubePlaylistMembership.Contains(youTubeService, playlistId, videoId);
    }

    public async Task<CatalogueParentMatch?> FindUnclaimedByYouTubeChannelTitle(string channelTitle)
    {
        if (string.IsNullOrWhiteSpace(channelTitle))
        {
            return null;
        }

        var title = channelTitle.Trim();
        var organisations = await PublisherNameAttachLookup.FindByName(newsOrganisations, title);
        var shows = await PublisherNameAttachLookup.FindByName(tvShows, title);
        var matches = organisations.Select(CatalogueParentMatch.For)
            .Concat(shows.Select(CatalogueParentMatch.For))
            .Where(x => !x.Parent.IsRemoved() && string.IsNullOrWhiteSpace(x.Indexing?.YouTubeChannelId))
            .ToList();
        return Single(matches, title);
    }

    private CatalogueParentMatch? Single(IReadOnlyList<CatalogueParentMatch> matches, string key)
    {
        if (matches.Count <= 1)
        {
            return matches.SingleOrDefault();
        }

        var kinds = matches.Select(x => x.ContentKind).Distinct().ToArray();
        var ids = matches.Select(x => x.Parent.Id).ToArray();
        logger.LogWarning(
            "'{Key}' matches {Count} news organisations / TV shows; refusing to pick one. Ids: {Ids}.",
            key, matches.Count, string.Join(", ", ids));
        throw new AmbiguousParentNameException(kinds.Length == 1 ? kinds[0] : MixedKind, key, ids);
    }
}
