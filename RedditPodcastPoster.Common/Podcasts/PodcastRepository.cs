using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastRepository : IPodcastRepository
{
    private const string ContainerName = "cultpodcasts";
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

    public async Task Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge, Action<Episode, Episode> merge)
    {
        foreach (var episodeToMerge in episodesToMerge)
        {
            var existingEpisode = podcast.Episodes.SingleOrDefault(x => Match(x, episodeToMerge));
            if (existingEpisode == null)
            {
                episodeToMerge.Id = Guid.NewGuid();
                podcast.Episodes.Add(episodeToMerge);
            }
            else
            {
                merge(existingEpisode, episodeToMerge);
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

        throw new InvalidOperationException(
            $"Unable to determine likeness of incoming episode with spotify-url {episodeToMerge.SpotifyId} and youtube-url {episodeToMerge.YouTubeId}");
    }


}