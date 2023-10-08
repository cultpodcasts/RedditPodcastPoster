using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Common.Matching;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastRepository : IPodcastRepository
{
    private readonly IDataRepository _dataRepository;
    private readonly IEpisodeMatcher _episodeMatcher;
    private readonly ILogger<PodcastRepository> _logger;

    public PodcastRepository(
        IDataRepository dataRepository,
        IEpisodeMatcher episodeMatcher,
        ILogger<PodcastRepository> logger)
    {
        _dataRepository = dataRepository;
        _episodeMatcher = episodeMatcher;
        _logger = logger;
    }

    public async Task<Podcast?> GetPodcast(string key)
    {
        var partitionKey = new Podcast().GetPartitionKey();
        return await _dataRepository.Read<Podcast>(key, partitionKey);
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

        podcast.Episodes = new List<Episode>(podcast.Episodes.OrderByDescending(x => x.Release));
        return new MergeResult(addedEpisodes, mergedEpisodes, failedEpisodes);
    }

    public IAsyncEnumerable<Podcast> GetAll()
    {
        return _dataRepository.GetAll<Podcast>();
    }

    public Task<IEnumerable<Guid>> GetAllIds()
    {
        return _dataRepository.GetAllIds<Podcast>(new Podcast().GetPartitionKey());
    }

    public async Task Save(Podcast podcast)
    {
        var key = podcast.GetPartitionKey();
        await _dataRepository.Write(key, podcast);
    }

    public async Task Update(Podcast podcast)
    {
        await Save(podcast);
    }

    private bool Match(Episode episode, Episode episodeToMerge, Regex? episodeMatchRegex)
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

        return _episodeMatcher.IsMatch(episode, episodeToMerge, episodeMatchRegex);
    }

    private bool Merge(Episode existingEpisode, Episode episodeToMerge)
    {
        var updated = false;
        if (existingEpisode.Urls.Spotify == null && episodeToMerge.Urls.Spotify != null)
        {
            existingEpisode.Urls.Spotify ??= episodeToMerge.Urls.Spotify;
            updated |= true;
        }

        if (existingEpisode.Urls.YouTube == null && episodeToMerge.Urls.YouTube != null)
        {
            existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
            updated |= true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
            updated |= true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            existingEpisode.YouTubeId = episodeToMerge.YouTubeId;
            updated |= true;
        }

        if (existingEpisode.Description.EndsWith("...") &&
            existingEpisode.Description.Length < episodeToMerge.Description.Length)
        {
            existingEpisode.Description = episodeToMerge.Description;
            updated |= true;
        }

        return updated;
    }
}