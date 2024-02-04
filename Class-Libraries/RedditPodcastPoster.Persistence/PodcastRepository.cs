﻿using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class PodcastRepository(
    IDataRepository dataRepository,
    IEpisodeMatcher episodeMatcher,
    ILogger<PodcastRepository> logger)
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

        podcast.Episodes = [..podcast.Episodes.OrderByDescending(x => x.Release)];
        return new MergeResult(addedEpisodes, mergedEpisodes, failedEpisodes);
    }

    public IAsyncEnumerable<Podcast> GetAll() => dataRepository.GetAll<Podcast>();

    public IAsyncEnumerable<Guid> GetAllIds() => dataRepository.GetAllIds<Podcast>();

    public Task Save(Podcast podcast) => dataRepository.Write(podcast);

    public Task Update(Podcast podcast) => Save(podcast);

    public Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector) => dataRepository.GetBy(selector);

    public IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector) => dataRepository.GetAllBy(selector);

    public IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item) => dataRepository.GetAllBy(selector, item);

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

        if (existingEpisode.Urls.YouTube == null && episodeToMerge.Urls.YouTube != null)
        {
            existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
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

        return updated;
    }
}