﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastRepository : IPodcastRepository
{
    private readonly IDataRepository _dataRepository;
    private readonly ILogger<PodcastRepository> _logger;

    public PodcastRepository(
        IDataRepository dataRepository,
        ILogger<PodcastRepository> logger)
    {
        _dataRepository = dataRepository;
        _logger = logger;
    }

    public async Task<Podcast?> GetPodcast(string key)
    {
        return await _dataRepository.Read<Podcast>(key);
    }

    public async Task Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge)
    {
        foreach (var episodeToMerge in episodesToMerge)
        {
            var existingEpisodes = podcast.Episodes.Where(x => Match(x, episodeToMerge));

            if (existingEpisodes.Count() == 1)
            {
                var existingEpisode = existingEpisodes.Single();
                if (existingEpisode == null)
                {
                    episodeToMerge.Id = Guid.NewGuid();
                    episodeToMerge.ModelType = ModelType.Episode;
                    podcast.Episodes.Add(episodeToMerge);
                }
                else
                {
                    Merge(existingEpisode, episodeToMerge);
                }
            }
            else
            {
                var multipleEpisodes = string.Join(",", existingEpisodes.Select(x => $"'{x.Title}'").ToArray());
                _logger.LogWarning($"Found multiple episode matches for podcast {podcast.Name}. Multiple episodes: {multipleEpisodes}");
            }
        }

        podcast.Episodes = new List<Episode>(podcast.Episodes.OrderByDescending(x => x.Release));
    }

    public IAsyncEnumerable<Podcast> GetAll()
    {
        return _dataRepository.GetAll<Podcast>();
    }

    public async Task Save(Podcast podcast)
    {
        var key = _dataRepository.KeySelector.GetKey(podcast);
        await _dataRepository.Write(key, podcast);
    }

    public async Task Update(Podcast podcast)
    {
        await Save(podcast);
    }

    private bool Match(Episode episode, Episode episodeToMerge)
    {
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId) && !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            return episode.SpotifyId == episodeToMerge.SpotifyId;
        }

        if (!string.IsNullOrWhiteSpace(episode.YouTubeId) && !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            return episode.YouTubeId == episodeToMerge.YouTubeId;
        }

        if (episode.AppleId.HasValue && episodeToMerge.AppleId.HasValue)
        {
            return episode.AppleId.Value == episodeToMerge.AppleId.Value;
        }
        

        if (!string.IsNullOrWhiteSpace(episode.Title) && !string.IsNullOrWhiteSpace(episodeToMerge.Title))
        {
            return episode.Title == episodeToMerge.Title;
        }

        _logger.LogWarning(
            $"Unable to determine likeness of incoming episode with spotify-url '{episodeToMerge.SpotifyId}' and youtube-url '{episodeToMerge.YouTubeId}'.");
        return false;
    }

    private void Merge(Episode existingEpisode, Episode episodeToMerge)
    {
        existingEpisode.Urls.Spotify ??= episodeToMerge.Urls.Spotify;
        existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            existingEpisode.YouTubeId = episodeToMerge.YouTubeId;
        }

        if (existingEpisode.Description.EndsWith("...") &&
            existingEpisode.Description.Length < episodeToMerge.Description.Length)
        {
            existingEpisode.Description = episodeToMerge.Description;
        }
    }
}