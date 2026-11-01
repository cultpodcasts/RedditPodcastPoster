using Microsoft.Extensions.Logging;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;
using Google.Apis.YouTube.v3.Data;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using PodcastEpisode = RedditPodcastPoster.Models.Episodes.PodcastEpisode;
using RedditPodcastPoster.Models.Serialization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public class YouTubeUrlCategoriser(
    IYouTubeServiceWrapper youTubeService,
    ITolerantYouTubeChannelService youTubeChannelService,
    ITolerantYouTubeVideoService youTubeVideoService,
    IYouTubeChannelReleaseBandSearch youTubeChannelReleaseBandSearch,
    IYouTubeChannelVideoRetrievalPolicy youTubeChannelVideoRetrievalPolicy,
    ITolerantYouTubePlaylistService youTubePlaylistService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    IEpisodeRepository episodeRepository,
    ILogger<YouTubeUrlCategoriser> logger)
    : IYouTubeUrlCategoriser
{
    private const int MultiplePublicationDateMatchTitleThreshold = 60;
    private const int TitleThreshold = 80;
    private const int SameTitleThreshold = 95;
    private static readonly long SameTitleDurationThreshold = TimeSpan.FromMinutes(2).Ticks;
    private static readonly TimeSpan PublishThreshold = TimeSpan.FromDays(2);

    public async Task<ResolvedYouTubeItem?> Resolve(
        Podcast? podcast,
        IList<EpisodeModel> podcastEpisodes,
        Uri url,
        IndexingContext indexingContext)
    {
        PodcastEpisode? pair = null;
        if (podcast != null && podcastEpisodes.Any(x =>
                EpisodeServicePresence.TryGetUrl(x, ServiceKeys.YouTube) == url))
        {
            var storedEpisodes = podcastEpisodes.Where(x =>
                EpisodeServicePresence.TryGetUrl(x, ServiceKeys.YouTube) == url).ToArray();
            if (storedEpisodes.Length > 1)
            {
                var ex = new InvalidOperationException(
                    $"Podcast '{podcast.Name}' with podcast-id '{podcast.Id}' has multiple episodes with url '{url}'.");
                logger.LogError(ex, ex.Message);
                throw ex;
            }

            var episode = storedEpisodes.Single();
            pair = new PodcastEpisode(podcast, episode);

            var episodes =
                await youTubeVideoService.GetVideoContentDetails(youTubeService, [YouTubeIdResolver.Extract(url)!],
                    indexingContext, true);
            if (episodes != null && episodes.Any())
            {
                if (episodes.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Multiple episodes retrieved from youtube video with url '{url}'.");
                }

                var description = episodes.First().Snippet.Description;
                if (pair.Episode.Description.Trim().EndsWith("...") &&
                    description.Length > pair.Episode.Description.Length)
                {
                    pair.Episode.Description = description;
                }
            }

            // A podcast with a playlist is a YouTube podcast without a channel id: the playlist is its only source.
            if (pair.Podcast.HasYouTubeSource())
            {
                return new ResolvedYouTubeItem(pair);
            }
        }

        var videoId = YouTubeIdResolver.Extract(url);
        if (videoId == null)
        {
            throw new InvalidOperationException($"Unable to find video-id in url '{url}'.");
        }

        var items = await youTubeVideoService.GetVideoContentDetails(youTubeService, [videoId], indexingContext, true);
        if (items != null)
        {
            var item = items.FirstOrDefault();
            if (item == null)
            {
                throw new InvalidOperationException($"Unable to find video with id '{videoId}'.");
            }

            var channel = await youTubeChannelService.GetChannel(new YouTubeChannelId(item.Snippet.ChannelId),
                indexingContext, true, true);
            var snippetChannelTitle = item.Snippet.ChannelTitle;
            var snippetDescription = channel!.Snippet.Description;

            var playlistId = YouTubePlaylistIdResolver.Extract(url);
            if (!string.IsNullOrWhiteSpace(playlistId))
            {
                GetPlaylistInfoResponse? playlist = null;
                try
                {
                    playlist = await youTubePlaylistService.GetPlaylistInfo(
                        new YouTubePlaylistId(playlistId, YouTubePlaylistIdSource.Unknown, "categoriser"),
                        indexingContext);
                }
                catch (Exception ex) when (ex is not OperationCanceledException ||
                                           !indexingContext.CancellationToken.IsCancellationRequested)
                {
                    // Private, deleted, or a WL/LL/RD mix, or the lookup failed: the video itself is fine, so submit it
                    // without the playlist rather than failing the whole submit.
                    logger.LogWarning(ex,
                        "Ignoring playlist-id '{PlaylistId}' from url '{Url}': its owner could not be resolved.",
                        playlistId, url);
                }

                if (playlist == null)
                {
                    playlistId = null;
                }
                else if (string.Equals(playlist.ChannelId, item.Snippet.ChannelId, StringComparison.Ordinal))
                {
                    snippetChannelTitle = playlist.Title;
                    snippetDescription = playlist.Description;
                }
                else
                {
                    // The URL's playlist is not the video channel's own (e.g. another channel's compilation, or a mix the
                    // video happens to be in). A new podcast must not store it: a playlist is the podcast's only YouTube
                    // source and every video in it is indexed, so an incidental list= would index someone else's videos.
                    // A curator can still set any channel's playlist deliberately in the edit dialog.
                    logger.LogWarning(
                        "Ignoring playlist-id '{PlaylistId}' from url '{Url}': it belongs to channel '{PlaylistChannelId}', not the video's channel '{VideoChannelId}'.",
                        playlistId, url, playlist.ChannelId, item.Snippet.ChannelId);
                    playlistId = null;
                }
            }

            if (channel != null)
            {
                if (pair == null)
                {
                    return new ResolvedYouTubeItem(
                        item.Snippet.ChannelId,
                        item.Id,
                        snippetChannelTitle,
                        snippetDescription,
                        channel.ContentOwnerDetails.ContentOwner,
                        item.Snippet.Title,
                        item.Snippet.Description,
                        item.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        item.GetLength() ?? TimeSpan.Zero,
                        item.ToYouTubeUrl(),
                        item.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
                        await youTubeThumbnailResolver.GetImageUrlAsync(item),
                        playlistId
                    );
                }

                // Reached only when the stored podcast has no YouTube source (a playlist podcast returned above).
                if (!pair.Podcast.HasYouTubeSource())
                {
                    pair.Podcast.YouTubeChannelId = item.Snippet.ChannelId;
                    return new ResolvedYouTubeItem(pair);
                }
            }
        }
        else
        {
            if (indexingContext.SkipYouTubeUrlResolving)
            {
                throw new InvalidOperationException(
                    $"Error: {nameof(indexingContext.SkipYouTubeUrlResolving)} be true.");
            }
        }

        return null;
    }

    public async Task<ResolvedYouTubeItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IList<EpisodeModel> episodes,
        IndexingContext indexingContext)
    {
        if (matchingPodcast != null && matchingPodcast.HasYouTubeSource())
        {
            string channelDescription = "", channelContentOwner = "";
            await HealYouTubeIdsExtractableFromUrls(matchingPodcast, episodes); // pragma: allowlist secret
            var mismatchedEpisodes = episodes.Where(HasInconsistentYouTubeIdAndUrl).ToArray();
            if (mismatchedEpisodes.Any())
            {
                throw new InvalidOperationException(
                    $"Podcast with id '{matchingPodcast.Id}' has episodes with inconsistent youtube-id && youtube-url. Episode-ids: {string.Join(", ", mismatchedEpisodes.Select(x => x.Id))}");
            }

            var expectedPublish = criteria.Release.Add(matchingPodcast.YouTubePublishingDelay());
            var band = EpisodeReleaseTolerance.GetSubmitMatchBand(expectedPublish);
            // The submit band is whole calendar days (EpisodeReleaseTolerance compares .Date), so its
            // bounds are UTC midnights by definition: relabel the day as UTC rather than converting it.
            // A Local-kind day must not shift to the previous UTC day (date-only release rule, #1212).
            var publishedAfter = UtcDateTimes.ToUtcOffset(DateTime.SpecifyKind(band.Start, DateTimeKind.Utc));
            var publishedBefore = UtcDateTimes.ToUtcOffset(DateTime.SpecifyKind(band.End.AddDays(1), DateTimeKind.Utc));
            var items = await LoadSubmitMatchCandidates(
                matchingPodcast,
                indexingContext,
                publishedAfter,
                publishedBefore);

            if (items == null || !items.Any())
            {
                return null;
            }

            var podcastEpisodeYouTubeIds = episodes
                .Select(x => EpisodeServicePresence.YouTubeEpisodeId(x))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            // Compare video ids: a playlist item's Id is the playlist-item id, not the video id.
            var unassignedChannelUploads =
                items.Where(x => !podcastEpisodeYouTubeIds.Contains(x.Snippet?.ResourceId?.VideoId ?? x.Id)).ToArray();
            var publishedWithin = PublishedWithinThreshold(unassignedChannelUploads, expectedPublish);
            PlaylistItem? match;
            if (publishedWithin.Any())
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, publishedWithin, x => x.Snippet.Title,
                    MultiplePublicationDateMatchTitleThreshold);
            }
            else
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, unassignedChannelUploads, x => x.Snippet.Title,
                    TitleThreshold);
            }

            if (match == null)
            {
                match = FuzzyMatcher.Match(criteria.EpisodeTitle, unassignedChannelUploads, x => x.Snippet.Title,
                    SameTitleThreshold);
                if (match != null)
                {
                    var videoContent =
                        await youTubeVideoService.GetVideoContentDetails(
                            youTubeService,
                            [match.Snippet.ResourceId.VideoId],
                            indexingContext
                        );
                    if (videoContent is { Count: 1 })
                    {
                        var duration = videoContent.Single().GetLength();
                        if (duration.HasValue)
                        {
                            var diff = Math.Abs((duration.Value - criteria.Duration).Ticks);
                            if (diff > SameTitleDurationThreshold)
                            {
                                match = null;
                            }
                        }
                        else
                        {
                            match = null;
                        }
                    }
                    else
                    {
                        match = null;
                    }
                }
            }

            if (match != null)
            {
                var channel = await youTubeChannelService.GetChannel(
                    new YouTubeChannelId(match.Snippet.ChannelId),
                    indexingContext,
                    true,
                    true);
                if (channel != null)
                {
                    channelDescription = channel.Snippet.Description;
                    channelContentOwner = channel.ContentOwnerDetails.ContentOwner;
                }

                var video = await youTubeVideoService.GetVideoContentDetails(youTubeService,
                    [match.Snippet.ResourceId.VideoId],
                    indexingContext,
                    true);
                if (video != null)
                {
                    var videoContent = video.SingleOrDefault();
                    return new ResolvedYouTubeItem(
                        match.Snippet.ChannelId,
                        match.Snippet.ResourceId.VideoId,
                        match.Snippet.ChannelTitle,
                        channelDescription, //
                        channelContentOwner, //
                        match.Snippet.Title,
                        match.Snippet.Description,
                        match.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                        videoContent?.GetLength() ?? TimeSpan.Zero,
                        match.Snippet.ToYouTubeUrl(),
                        videoContent?.ContentDetails.ContentRating.YtRating == "ytAgeRestricted",
                        videoContent != null
                            ? await youTubeThumbnailResolver.GetImageUrlAsync(videoContent)
                            : null,
                        string.Empty);
                }
            }
        }
        else
        {
            if (matchingPodcast != null)
            {
                logger.LogInformation(
                    "Podcast with id '{podcastId}' has no youtube channel-id or playlist-id.", matchingPodcast.Id);
            }
        }

        return null;
    }

    private async Task<IList<PlaylistItem>?> LoadSubmitMatchCandidates(
        Podcast matchingPodcast,
        IndexingContext indexingContext,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore)
    {
        // The playlist is the podcast's only YouTube source when set, so the match is searched in it rather than in the
        // channel (whose other uploads are not the podcast's, and whose id may be unset or another channel's).
        if (matchingPodcast.UsesYouTubePlaylist())
        {
            return await LoadBandBoundedPlaylist(
                matchingPodcast, indexingContext, publishedAfter, publishedBefore, "youTubePlaylist");
        }

        var uploadsPlaylistReason = youTubeChannelVideoRetrievalPolicy.GetUploadsPlaylistReason(matchingPodcast);
        if (uploadsPlaylistReason != null)
        {
            return await LoadBandBoundedPlaylist(
                matchingPodcast, indexingContext, publishedAfter, publishedBefore, uploadsPlaylistReason);
        }

        try
        {
            var bandResults = await youTubeChannelReleaseBandSearch.Search(
                matchingPodcast.YouTubeChannelId,
                publishedAfter,
                publishedBefore,
                indexingContext);
            return bandResults?
                .Where(x => x.Id?.VideoId != null && x.Snippet != null)
                .Select(x => new PlaylistItem
                {
                    Id = x.Id.VideoId,
                    Snippet = new PlaylistItemSnippet
                    {
                        Title = x.Snippet.Title,
                        Description = x.Snippet.Description,
                        ChannelId = x.Snippet.ChannelId,
                        ChannelTitle = x.Snippet.ChannelTitle,
                        PublishedAtDateTimeOffset = x.Snippet.PublishedAtDateTimeOffset,
                        ResourceId = new ResourceId { VideoId = x.Id.VideoId }
                    }
                })
                .ToList();
        }
        catch (YouTubeChannelSearchForbiddenException ex)
        {
            logger.LogInformation(ex,
                "Search.List is not permitted for channel-id '{ChannelId}'; falling back to the playlist.",
                matchingPodcast.YouTubeChannelId);
            matchingPodcast.YouTubeChannelSearchForbidden = true;
            return await LoadBandBoundedPlaylist(
                matchingPodcast,
                indexingContext,
                publishedAfter,
                publishedBefore,
                "youTubeChannelSearchForbidden");
        }
    }

    /// <summary>
    /// Playlist and uploads walks have a floor and no ceiling. The floor is the band start so paging
    /// stops once publish times are older than it. A years-old band still pages back to that date.
    /// Items newer than the band end are dropped here.
    /// </summary>
    private async Task<IList<PlaylistItem>?> LoadBandBoundedPlaylist(
        Podcast matchingPodcast,
        IndexingContext indexingContext,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore,
        string reason)
    {
        logger.LogInformation(
            "Submit match paging playlist until publish times are older than {BandStart:u} for channel '{ChannelId}' playlist '{PlaylistId}' ({Reason}). Items newer than {BandEnd:u} are dropped.",
            publishedAfter,
            matchingPodcast.YouTubeChannelId,
            matchingPodcast.YouTubePlaylistId,
            reason,
            publishedBefore);

        var playlistId = await ResolveFallbackPlaylistId(matchingPodcast, indexingContext);
        if (playlistId == null)
        {
            return null;
        }

        var playlistContext = indexingContext with { ReleasedSince = publishedAfter.UtcDateTime };
        var response = await youTubePlaylistService.GetPlaylistVideoSnippets(
            playlistId,
            playlistContext,
            expensivePlaylist: matchingPodcast.HasExpensiveYouTubePlaylistQuery(),
            // An arbitrary-order playlist cannot stop at the band floor; the uploads playlist is always newest-first.
            playlistOrder: playlistId.Source == YouTubePlaylistIdSource.PodcastEntity
                ? matchingPodcast.YouTubePlaylistOrder
                : null);
        return WithinSubmitMatchBand(response.Result, publishedAfter, publishedBefore);
    }

    private async Task<YouTubePlaylistId?> ResolveFallbackPlaylistId(
        Podcast matchingPodcast,
        IndexingContext indexingContext)
    {
        if (!string.IsNullOrWhiteSpace(matchingPodcast.YouTubePlaylistId))
        {
            return new YouTubePlaylistId(
                matchingPodcast.YouTubePlaylistId,
                YouTubePlaylistIdSource.PodcastEntity,
                matchingPodcast.Id.ToString());
        }

        var channel = await youTubeChannelService.GetChannel(
            new YouTubeChannelId(matchingPodcast.YouTubeChannelId),
            indexingContext,
            withContentDetails: true);
        var uploadsId = channel?.ContentDetails?.RelatedPlaylists?.Uploads;
        if (string.IsNullOrWhiteSpace(uploadsId))
        {
            logger.LogInformation(
                "No uploads playlist for channel-id '{ChannelId}'.",
                matchingPodcast.YouTubeChannelId);
            return null;
        }

        return new YouTubePlaylistId(
            uploadsId,
            YouTubePlaylistIdSource.ChannelUploads,
            matchingPodcast.YouTubeChannelId);
    }

    private static IList<PlaylistItem>? WithinSubmitMatchBand(
        IList<PlaylistItem>? items,
        DateTimeOffset publishedAfter,
        DateTimeOffset publishedBefore)
    {
        if (items == null)
        {
            return null;
        }

        return items.Where(item =>
        {
            var published = item.ContentDetails?.VideoPublishedAtDateTimeOffset
                            ?? item.Snippet?.PublishedAtDateTimeOffset;
            return published >= publishedAfter && published < publishedBefore;
        }).ToList();
    }

    /// <summary>
    /// A live episode whose YouTube url contains a video id but whose nested id is empty is healed
    /// (<see cref="EpisodeServicePresence.FillYouTubeIdFromUrl"/>) on the instance in the list, then // pragma: allowlist secret
    /// re-read and saved from that fresh copy. Search reads only the nested id, so an in-memory fill // pragma: allowlist secret
    /// that is not saved leaves the Watch icon missing. The list was loaded before the other platform
    /// calls, and Save upserts the whole document, so the loaded instance is not what gets written.
    /// Rows this cannot reconcile stay for <see cref="HasInconsistentYouTubeIdAndUrl"/>.
    /// </summary>
    private async Task HealYouTubeIdsExtractableFromUrls(Podcast podcast, IList<EpisodeModel> episodes) // pragma: allowlist secret
    {
        foreach (var episode in episodes) // pragma: allowlist secret
        {
            if (!HasInconsistentYouTubeIdAndUrl(episode))
            {
                continue;
            }

            if (!EpisodeServicePresence.FillYouTubeIdFromUrl(episode)) // pragma: allowlist secret
            {
                continue;
            }

            var current = await episodeRepository.GetEpisode(episode.PodcastId, episode.Id);
            if (current is null || !EpisodeServicePresence.FillYouTubeIdFromUrl(current)) // pragma: allowlist secret
            {
                continue;
            }

            logger.LogInformation(
                "Healed episode '{EpisodeId}' on podcast '{PodcastId}': filled missing ids.youtube from the video id in the YouTube url.",
                episode.Id,
                podcast.Id);
            await episodeRepository.Save(current);
        }
    }

    /// <summary>
    /// True when the stored YouTube id and URL disagree, or only one of them is present
    /// (except a removed episode that has a URL and no stored id).
    /// Compares the nested stored id, not the read fallback that extracts a video id from the URL.
    /// </summary>
    internal static bool HasInconsistentYouTubeIdAndUrl(EpisodeModel episode)
    {
        var youTubeId = EpisodeServicePresence.NestedYouTubeId(episode); // pragma: allowlist secret
        var youTubeUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube);
        var idMissing = string.IsNullOrWhiteSpace(youTubeId);
        return (!episode.IsRemoved() && idMissing && youTubeUrl is not null) ||
               (youTubeUrl is null && !idMissing) ||
               (!idMissing && youTubeUrl is not null &&
                YouTubeIdResolver.Extract(youTubeUrl) != youTubeId);
    }

    /// <summary>
    /// The uploads published within <see cref="PublishThreshold"/> either side of <paramref name="expectedPublish"/>.
    /// <paramref name="expectedPublish"/> is UTC (unspecified kind is UTC, #1194), never host-local time.
    /// </summary>
    internal static PlaylistItem[] PublishedWithinThreshold(IEnumerable<PlaylistItem> uploads, DateTime expectedPublish)
    {
        var expected = UtcDateTimes.ToUtcOffset(expectedPublish);
        return uploads.Where(x =>
                x.Snippet.PublishedAtDateTimeOffset > expected.Subtract(PublishThreshold) &&
                x.Snippet.PublishedAtDateTimeOffset < expected.Add(PublishThreshold))
            .ToArray();
    }
}
