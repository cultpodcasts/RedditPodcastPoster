using System.Linq.Expressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

public sealed class InMemoryEpisodeRepository : IEpisodeRepository
{
    private readonly Dictionary<Guid, Episode> _episodes = new();
    private readonly SaveCallRecorder? _saveCallRecorder;

    public InMemoryEpisodeRepository(SaveCallRecorder? saveCallRecorder = null)
    {
        _saveCallRecorder = saveCallRecorder;
    }

    private readonly List<Episode> _savedEpisodes = [];

    public IReadOnlyList<Episode> SavedEpisodes => _savedEpisodes;

    public void Seed(params Episode[] episodes)
    {
        foreach (var episode in episodes)
        {
            _episodes[episode.Id] = Clone(episode);
        }
    }

    public Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId)
    {
        if (!_episodes.TryGetValue(episodeId, out var episode) || episode.PodcastId != podcastId)
        {
            return Task.FromResult<Episode?>(null);
        }

        return Task.FromResult<Episode?>(Clone(episode));
    }

    public Task<int> Count(Guid podcastId) =>
        Task.FromResult(_episodes.Values.Count(x => x.PodcastId == podcastId));

    public Task<int> Count() => Task.FromResult(_episodes.Count);

    public async IAsyncEnumerable<Episode> GetAll()
    {
        foreach (var episode in _episodes.Values.Select(Clone))
        {
            yield return episode;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId)
    {
        foreach (var episode in _episodes.Values.Where(x => x.PodcastId == podcastId).Select(Clone))
        {
            yield return episode;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<Episode> GetByPodcastId(
        Guid podcastId,
        Expression<Func<Episode, bool>> selector)
    {
        var predicate = selector.Compile();
        foreach (var episode in _episodes.Values
                     .Where(x => x.PodcastId == podcastId)
                     .Where(predicate)
                     .Select(Clone))
        {
            yield return episode;
        }

        await Task.CompletedTask;
    }

    public Task<Episode?> GetMostRecentByPodcastId(Guid podcastId)
    {
        var mostRecent = _episodes.Values
            .Where(x => x.PodcastId == podcastId)
            .MaxBy(x => x.Release);
        return Task.FromResult(mostRecent is null ? null : Clone(mostRecent));
    }

    public Task Save(Episode entity)
    {
        _episodes[entity.Id] = Clone(entity);
        _savedEpisodes.Add(Clone(entity));
        _saveCallRecorder?.RecordSingle(entity.Id);
        return Task.CompletedTask;
    }

    public Task Save(IEnumerable<Episode> episodes)
    {
        var saved = episodes.Select(Clone).ToList();
        foreach (var episode in saved)
        {
            _episodes[episode.Id] = episode;
            _savedEpisodes.Add(episode);
        }

        _saveCallRecorder?.RecordBatch(saved.Select(x => x.Id));
        return Task.CompletedTask;
    }

    public Task Delete(Guid podcastId, Guid episodeId)
    {
        if (_episodes.TryGetValue(episodeId, out var episode) && episode.PodcastId == podcastId)
        {
            _episodes.Remove(episodeId);
        }

        return Task.CompletedTask;
    }

    public Task<Episode?> GetBy(Expression<Func<Episode, bool>> selector)
    {
        var predicate = selector.Compile();
        var match = _episodes.Values.FirstOrDefault(predicate);
        return Task.FromResult(match is null ? null : Clone(match));
    }

    public async IAsyncEnumerable<Episode> GetAllBy(Expression<Func<Episode, bool>> selector)
    {
        var predicate = selector.Compile();
        foreach (var episode in _episodes.Values.Where(predicate).Select(Clone))
        {
            yield return episode;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Episode, bool>> selector,
        Expression<Func<Episode, TProjection>> projection)
    {
        var predicate = selector.Compile();
        var project = projection.Compile();
        foreach (var episode in _episodes.Values.Where(predicate))
        {
            yield return project(episode);
        }

        await Task.CompletedTask;
    }

    public Task PatchGuests(Guid podcastId, Guid episodeId, string[] guests)
    {
        ArgumentNullException.ThrowIfNull(guests);
        if (!_episodes.TryGetValue(episodeId, out var episode) || episode.PodcastId != podcastId)
        {
            throw new InvalidOperationException(
                $"Episode {episodeId} not found for podcast {podcastId}.");
        }

        episode.Guests = guests.ToArray();
        return Task.CompletedTask;
    }

    public Episode GetStored(Guid episodeId) =>
        Clone(_episodes[episodeId]);

    private static Episode Clone(Episode episode) =>
        new()
        {
            Id = episode.Id,
            PodcastId = episode.PodcastId,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Removed = episode.Removed,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = new ServiceUrls
            {
                Spotify = episode.Urls.Spotify,
                Apple = episode.Urls.Apple,
                YouTube = episode.Urls.YouTube,
                BBC = episode.Urls.BBC,
                InternetArchive = episode.Urls.InternetArchive
            },
            Images = episode.Images is null
                ? null
                : new EpisodeImages
                {
                    Spotify = episode.Images.Spotify,
                    Apple = episode.Images.Apple,
                    YouTube = episode.Images.YouTube,
                    Other = episode.Images.Other
                },
            Subjects = [.. episode.Subjects],
            SearchTerms = episode.SearchTerms,
            PodcastName = episode.PodcastName,
            PodcastSearchTerms = episode.PodcastSearchTerms,
            PodcastLanguage = episode.PodcastLanguage,
            Language = episode.Language,
            PodcastMetadataVersion = episode.PodcastMetadataVersion,
            PodcastRemoved = episode.PodcastRemoved,
            Guests = episode.Guests?.ToArray(),
            TwitterHandles = episode.TwitterHandles?.ToArray(),
            BlueskyHandles = episode.BlueskyHandles?.ToArray(),
            Timestamp = episode.Timestamp
        };
}
