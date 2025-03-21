﻿using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class PodcastRepository(
    IDataRepository dataRepository,
    IEpisodeMatcher episodeMatcher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastRepository> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IPodcastRepository
{
    public Task<Podcast?> GetPodcast(Guid podcastId)
    {
        return dataRepository.Read<Podcast>(podcastId.ToString());
    }

    public MergeResult Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge)
    {
        Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, RegexOptions.Compiled);
        }

        var addedEpisodes = new List<Episode>();
        var mergedEpisodes = new List<(Episode Existing, Episode NewDetails)>();
        var failedEpisodes = new List<IEnumerable<Episode>>();
        foreach (var episodeToMerge in episodesToMerge)
        {
            var existingEpisodes = podcast.Episodes.Where(x => Match(x, episodeToMerge, episodeMatchRegex));

            if (existingEpisodes.Count() <= 1)
            {
                var existingEpisode = existingEpisodes.SingleOrDefault();
                if (existingEpisode == null)
                {
                    episodeToMerge.Id = Guid.NewGuid();
                    episodeToMerge.ModelType = ModelType.Episode;
                    podcast.Episodes.Add(episodeToMerge);
                    addedEpisodes.Add(episodeToMerge);
                }
                else
                {
                    var updated = Merge(existingEpisode, episodeToMerge);
                    if (updated)
                    {
                        mergedEpisodes.Add((Existing: existingEpisode, NewDetails: episodeToMerge));
                    }
                }
            }
            else
            {
                failedEpisodes.Add(existingEpisodes);
            }
        }

        podcast.Episodes = [.. podcast.Episodes.OrderByDescending(x => x.Release)];
        return new MergeResult(addedEpisodes, mergedEpisodes, failedEpisodes);
    }

    public IAsyncEnumerable<Podcast> GetAll()
    {
        return dataRepository.GetAll<Podcast>();
    }

    public IAsyncEnumerable<Guid> GetAllIds()
    {
        return dataRepository.GetAllIds<Podcast>();
    }

    public IAsyncEnumerable<string> GetAllFileKeys()
    {
        return dataRepository.GetAllFileKeys<Podcast>();
    }

    public Task Save(Podcast podcast)
    {
        return dataRepository.Write(podcast);
    }

    public Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector)
    {
        return dataRepository.GetBy(selector);
    }

    public Task<T?> GetBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item)
    {
        return dataRepository.GetBy(selector, item);
    }

    public IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector)
    {
        return dataRepository.GetAllBy(selector);
    }


    public IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item)
    {
        return dataRepository.GetAllBy(selector, item);
    }

    public async Task<IEnumerable<Guid>> GetPodcastsIdsWithUnpostedReleasedSince(DateTime since)
    {
        var items = await GetAllBy(x =>
            (!x.Removed.IsDefined() || x.Removed == false) &&
            x.Episodes.Any(episode =>
                episode.Release >= since &&
                episode.Posted == false &&
                episode.Ignored == false &&
                episode.Removed == false), x => new {guid = x.Id}).ToListAsync();
        return items.Select(x => x.guid);
    }

    public async Task<IEnumerable<Guid>> GetPodcastIdsWithUntweetedReleasedSince(DateTime since)
    {
        var items = await GetAllBy(x =>
            (!x.Removed.IsDefined() || x.Removed == false) &&
            x.Episodes.Any(episode =>
                episode.Release >= since &&
                episode.Tweeted == false &&
                episode.Ignored == false &&
                episode.Removed == false), x => new {guid = x.Id}).ToListAsync();
        return items.Select(x => x.guid);
    }

    public async Task<IEnumerable<Guid>> GetPodcastIdsWithBlueskyReadyReleasedSince(DateTime since)
    {
        var items = await GetAllBy(x =>
            (!x.Removed.IsDefined() || x.Removed == false) &&
            x.Episodes.Any(episode =>
                episode.Release >= since &&
                (!episode.BlueskyPosted.IsDefined() || episode.BlueskyPosted == false) &&
                episode.Ignored == false &&
                episode.Removed == false), x => new {guid = x.Id}).ToListAsync();
        return items.Select(x => x.guid);
    }

    public async Task<bool> PodcastHasEpisodesAwaitingEnrichment(Guid podcastId, DateTime since)
    {
        var podcastPublishDelay = await GetBy(podcast =>
                (!podcast.Removed.IsDefined() || podcast.Removed == false) &&
                podcast.Id == podcastId,
            x => new {delay = x.YouTubePublicationOffset});
        if (podcastPublishDelay != null)
        {
            if (podcastPublishDelay.delay.HasValue)
            {
                var delay = TimeSpan.FromTicks(Math.Abs(podcastPublishDelay.delay.Value));
                since -= delay;
            }

            var item = await GetBy(podcast =>
                    (!podcast.Removed.IsDefined() || podcast.Removed == false) &&
                    podcast.Id == podcastId &&
                    podcast.Episodes.Any(episode =>
                        episode.Release >= since &&
                        (
                            (podcast.SpotifyId != string.Empty && episode.SpotifyId == string.Empty) ||
                            (podcast.YouTubeChannelId != string.Empty && episode.YouTubeId == string.Empty) ||
                            (podcast.AppleId.IsDefined() && podcast.AppleId > 0 &&
                             (!episode.AppleId.IsDefined() || episode.AppleId == 0))
                        )
                    ),
                x => new {x.Id}
            );
            return item != null;
        }

        return false;
    }

    private bool Match(Episode episode, Episode episodeToMerge, Regex? episodeMatchRegex)
    {
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId) && !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            if (episode.SpotifyId == episodeToMerge.SpotifyId)
            {
                return true;
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(episode.YouTubeId) && !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            if (episode.YouTubeId == episodeToMerge.YouTubeId)
            {
                return true;
            }

            return false;
        }

        if (episode.AppleId.HasValue && episodeToMerge.AppleId.HasValue)
        {
            if (episode.AppleId.Value == episodeToMerge.AppleId.Value)
            {
                return true;
            }

            return false;
        }

        return episodeMatcher.IsMatch(episode, episodeToMerge, episodeMatchRegex);
    }

    private bool Merge(Episode existingEpisode, Episode episodeToMerge)
    {
        var updated = false;
        if (existingEpisode.Urls.Spotify == null && episodeToMerge.Urls.Spotify != null)
        {
            existingEpisode.Urls.Spotify ??= episodeToMerge.Urls.Spotify;
            updated = true;
        }

        if (existingEpisode.Images?.Spotify == null && episodeToMerge.Images?.Spotify != null)
        {
            existingEpisode.Images ??= new EpisodeImages();
            existingEpisode.Images.Spotify ??= episodeToMerge.Images.Spotify;
            updated = true;
        }

        if (existingEpisode.Urls.Apple == null && episodeToMerge.Urls.Apple != null)
        {
            existingEpisode.Urls.Apple ??= episodeToMerge.Urls.Apple;
            updated = true;
        }

        if (existingEpisode.Images?.Apple == null && episodeToMerge.Images?.Apple != null)
        {
            existingEpisode.Images ??= new EpisodeImages();
            existingEpisode.Images.Apple ??= episodeToMerge.Images.Apple;
            updated = true;
        }

        if (existingEpisode.Urls.YouTube == null && episodeToMerge.Urls.YouTube != null)
        {
            existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
            updated = true;
        }

        if (existingEpisode.Images?.YouTube == null && episodeToMerge.Images?.YouTube != null)
        {
            existingEpisode.Images ??= new EpisodeImages();
            existingEpisode.Images.YouTube ??= episodeToMerge.Images.YouTube;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
            updated = true;
        }

        if (existingEpisode.AppleId == null && episodeToMerge.AppleId != null)
        {
            existingEpisode.AppleId = episodeToMerge.AppleId;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            existingEpisode.YouTubeId = episodeToMerge.YouTubeId;
            updated = true;
        }

        if (existingEpisode.Description.EndsWith("...") &&
            existingEpisode.Description.Length < episodeToMerge.Description.Length)
        {
            existingEpisode.Description = episodeToMerge.Description;
            updated = true;
        }

        if (existingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
            episodeToMerge.Release.TimeOfDay > TimeSpan.Zero)
        {
            existingEpisode.Release = episodeToMerge.Release;
            updated = true;
        }

        return updated;
    }
}