using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.UrlSubmission.Services;

public class PodcastService(
    IYouTubeServiceWrapper youTubeService,
    IPodcastRepository podcastRepository,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IYouTubeVideoService youTubeVideoService,
    INonPodcastServiceAdapterResolver nonPodcastServiceAdapterResolver,
    IYouTubePlaylistMembership youTubePlaylistMembership,
    ICatalogueParentLookup catalogueParentLookup,
    ILogger<PodcastService> logger
) : IPodcastService
{
    public async Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext) =>
        (await GetEpisodeUrlOwner(url, indexingContext)).Podcast;

    public async Task<EpisodeUrlOwner> GetEpisodeUrlOwner(Uri url, IndexingContext indexingContext)
    {
        var ids = await GetOwnerIds(url, indexingContext);
        var podcast = string.IsNullOrWhiteSpace(ids.YouTubeChannelId)
            ? await MatchPodcast(ids)
            : await MatchYouTubeChannelPodcast(ids, url, indexingContext);
        return new EpisodeUrlOwner(podcast, ids);
    }

    public Task<bool?> IsVideoInPodcastPlaylist(Podcast podcast, Uri url, IndexingContext indexingContext) =>
        IsVideoInPlaylist(podcast.YouTubePlaylistId, url, indexingContext);

    /// <summary>
    /// True when <paramref name="playlistId"/> is blank (null and empty are the same: channel-wide), the URL's
    /// <c>list=</c> is the playlist, or YouTube lists the video in it. False when not in it; null when unknown.
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

        var videoId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(videoId) || indexingContext.SkipYouTubeUrlResolving)
        {
            return null;
        }

        return await youTubePlaylistMembership.Contains(youTubeService, playlistId, videoId);
    }

    /// <summary>
    /// Picks the podcast that owns a YouTube video from the podcasts on its channel:
    /// <list type="number">
    /// <item>Removed podcasts never compete with live ones; a channel with only removed podcasts still returns
    /// one, so the submit is refused as removed rather than re-creating the podcast.</item>
    /// <item>A podcast whose playlist contains the video wins (null and empty playlist ids are both channel-wide).
    /// </item>
    /// <item>Otherwise a news organisation or TV show that claims the channel (channel-wide, or its playlist
    /// contains the video) beats a channel-wide podcast: returns null so the router picks the parent.</item>
    /// <item>Otherwise the channel-wide podcast (indexAll / title-regex / channel-title tie-breaks).</item>
    /// <item>Otherwise the only playlist podcast on the channel keeps the URL (legacy), and several playlist
    /// podcasts none of which contain the video are ambiguous.</item>
    /// </list>
    /// </summary>
    private async Task<Podcast?> MatchYouTubeChannelPodcast(
        EpisodeUrlOwnerIds ids, Uri url, IndexingContext indexingContext)
    {
        var channelId = ids.YouTubeChannelId!;
        var all = await podcastRepository.GetAllBy(podcast => podcast.YouTubeChannelId == channelId).ToArrayAsync();
        if (all.Length == 0)
        {
            return null;
        }

        var live = all.Where(x => !x.IsRemoved()).ToArray();
        if (live.Length == 0)
        {
            return all.OrderBy(x => x.Id).First();
        }

        var playlistPodcasts = live.Where(x => !string.IsNullOrWhiteSpace(x.YouTubePlaylistId)).ToArray();
        var channelWide = live.Where(x => string.IsNullOrWhiteSpace(x.YouTubePlaylistId)).ToArray();

        var containing = new List<Podcast>();
        var unknown = new List<Podcast>();
        foreach (var candidate in playlistPodcasts)
        {
            var inPlaylist = await IsVideoInPlaylist(candidate.YouTubePlaylistId, url, indexingContext);
            if (inPlaylist == true)
            {
                containing.Add(candidate);
            }
            else if (inPlaylist == null)
            {
                unknown.Add(candidate);
            }
        }

        if (containing.Count == 1)
        {
            return containing[0];
        }

        if (containing.Count > 1)
        {
            return BreakChannelTie(containing, ids, channelId);
        }

        if (unknown.Count > 0)
        {
            var message =
                $"Could not check whether '{url}' is in the playlist of podcast(s) " +
                $"{string.Join(", ", unknown.Select(x => $"'{x.Id}'"))} on youtube-channel-id '{channelId}'.";
            var exception = new InvalidOperationException(message);
            logger.LogError(exception, message);
            throw exception;
        }

        var parent = await catalogueParentLookup.FindByOwner(ids);
        var parentClaims = parent == null
            ? false
            : await IsVideoInPlaylist(parent.Indexing?.YouTubePlaylistId, url, indexingContext);
        if (parentClaims == null)
        {
            var message =
                $"Could not check whether '{url}' is in the playlist of {parent!.Kind} '{parent.Parent.Id}' on youtube-channel-id '{channelId}'.";
            var exception = new InvalidOperationException(message);
            logger.LogError(exception, message);
            throw exception;
        }

        if (parentClaims == true)
        {
            if (channelWide.Length > 0)
            {
                logger.LogWarning(
                    "{Kind} '{ParentId}' claims youtube-channel-id '{ChannelId}', so '{Url}' goes to it rather than channel-wide podcast(s) {PodcastIds}. Give those podcasts a youtube-playlist-id.",
                    parent!.Kind, parent.Parent.Id, channelId, url, string.Join(", ", channelWide.Select(x => x.Id)));
            }

            return null;
        }

        if (channelWide.Length > 0)
        {
            return BreakChannelTie(channelWide, ids, channelId);
        }

        if (playlistPodcasts.Length == 1)
        {
            logger.LogInformation(
                "'{Url}' is not in the playlist of podcast '{PodcastId}', the only podcast on youtube-channel-id '{ChannelId}'; keeping it as the owner.",
                url, playlistPodcasts[0].Id, channelId);
            return playlistPodcasts[0];
        }

        var ambiguous =
            $"'{url}' is not in the playlist of any of the {playlistPodcasts.Length} podcasts on youtube-channel-id '{channelId}'. Submit it with a podcast name.";
        var ambiguousException = new InvalidOperationException(ambiguous);
        logger.LogError(ambiguousException, ambiguous);
        throw ambiguousException;
    }

    private Podcast BreakChannelTie(IReadOnlyList<Podcast> candidates, EpisodeUrlOwnerIds ids, string channelId)
    {
        IEnumerable<Podcast> matches = candidates;
        if (matches.Count() > 1 && matches.Count(x =>
                x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex)) == 1)
        {
            matches = matches.Where(x => x.IndexAllEpisodes || !string.IsNullOrWhiteSpace(x.EpisodeIncludeTitleRegex));
        }

        if (matches.Count() > 1)
        {
            var videoChannelName = (ids.YouTubeChannelTitle ?? string.Empty).Trim().ToLowerInvariant();
            matches = matches.Where(x => x.Name.Trim().ToLowerInvariant() == videoChannelName);
            if (matches.Count() != 1)
            {
                var message = $"Multiple podcasts with youtube-channel-id '{channelId}'.";
                var invalidOperationException = new InvalidOperationException(message);
                logger.LogError(invalidOperationException, message);
                throw invalidOperationException;
            }
        }

        return matches.Single();
    }

    public async Task<EpisodeUrlOwnerIds> GetOwnerIds(Uri url, IndexingContext indexingContext)
    {
        if (SpotifyPodcastServiceMatcher.IsMatch(url))
        {
            var episodeId = SpotifyIdResolver.GetEpisodeId(url);
            if (string.IsNullOrWhiteSpace(episodeId))
            {
                throw new ArgumentException($"Unable to extract spotify-episode-id from '{url}'.", nameof(url));
            }

            var findSpotifyEpisodeRequest = new FindSpotifyEpisodeRequest(string.Empty, string.Empty,
                episodeId, string.Empty, null, false, Market: Market.CountryCode);
            var episode = await spotifyEpisodeResolver.FindEpisode(findSpotifyEpisodeRequest, indexingContext);
            if (episode.FullEpisode == null)
            {
                throw new InvalidOperationException(
                    $"Unable to find spotify-full-show for spotify-episode with spotify-episode-id '{episodeId}'.");
            }

            return new EpisodeUrlOwnerIds(SpotifyShowId: episode.FullEpisode.Show.Id);
        }

        if (YouTubePodcastServiceMatcher.IsMatch(url))
        {
            var videoId = YouTubeIdResolver.Extract(url);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException($"Unable to extract youtube-video-id from '{url}'.", nameof(url));
            }

            var episodes =
                await youTubeVideoService.GetVideoContentDetails(youTubeService, [videoId], indexingContext, true);
            if (episodes == null || !episodes.Any())
            {
                throw new InvalidOperationException($"Unable to find youtube-video for youtube-video-id '{videoId}'.");
            }

            var snippet = episodes.First().Snippet;
            return new EpisodeUrlOwnerIds(YouTubeChannelId: snippet.ChannelId, YouTubeChannelTitle: snippet.ChannelTitle);
        }

        if (ApplePodcastServiceMatcher.IsMatch(url))
        {
            var podcastId = AppleIdResolver.GetPodcastId(url);
            if (podcastId == null)
            {
                throw new ArgumentException($"Unable to extract apple-episode-id from '{url}'.", nameof(url));
            }

            return new EpisodeUrlOwnerIds(ApplePodcastId: podcastId);
        }

        if (nonPodcastServiceAdapterResolver.ForSubmit(url) != null)
        {
            return EpisodeUrlOwnerIds.None;
        }

        throw new ArgumentException($"Unable to determine service for url '{url}'.", nameof(url));
    }

    private async Task<Podcast?> MatchPodcast(EpisodeUrlOwnerIds ids)
    {
        IEnumerable<Podcast> v2Podcasts;
        if (ids.SpotifyShowId is { } spotifyShowId)
        {
            v2Podcasts = await podcastRepository.GetAllBy(podcast => podcast.SpotifyId == spotifyShowId)
                .ToArrayAsync();
        }
        else if (ids.ApplePodcastId is { } podcastId)
        {
            v2Podcasts = await podcastRepository.GetAllBy(podcast => podcast.AppleId == podcastId).ToArrayAsync();
        }
        else
        {
            v2Podcasts = [];
        }

        if (v2Podcasts.Count() > 1 && v2Podcasts.Any(x => !x.IsRemoved()))
        {
            // A removed podcast never competes with a live one for the same show.
            v2Podcasts = v2Podcasts.Where(x => !x.IsRemoved());
        }

        if (v2Podcasts.Count() > 1)
        {
            v2Podcasts = v2Podcasts.Where(x => x.IndexAllEpisodes);
        }

        var v2Podcast = v2Podcasts.SingleOrDefault();
        return v2Podcast;
    }
}
