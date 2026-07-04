using System.Linq.Expressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

public sealed class InMemoryPodcastRepository : IPodcastRepository
{
    private readonly Dictionary<Guid, Podcast> _podcasts = new();

    private readonly List<Podcast> _savedPodcasts = [];

    public IReadOnlyList<Podcast> SavedPodcasts => _savedPodcasts;

    public void Seed(params Podcast[] podcasts)
    {
        foreach (var podcast in podcasts)
        {
            _podcasts[podcast.Id] = Clone(podcast);
        }
    }

    public Task<Podcast?> GetPodcast(Guid podcastId) =>
        Task.FromResult(_podcasts.TryGetValue(podcastId, out var podcast) ? Clone(podcast) : null);

    public Task Save(Podcast entity)
    {
        _podcasts[entity.Id] = Clone(entity);
        _savedPodcasts.Add(Clone(entity));
        return Task.CompletedTask;
    }

    public Task<int> Count() => Task.FromResult(_podcasts.Count);

    public async IAsyncEnumerable<Podcast> GetAll()
    {
        foreach (var podcast in _podcasts.Values.Select(Clone))
        {
            yield return podcast;
        }

        await Task.CompletedTask;
    }

    public Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector)
    {
        var predicate = selector.Compile();
        var match = _podcasts.Values.FirstOrDefault(predicate);
        return Task.FromResult(match is null ? null : Clone(match));
    }

    public async IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector)
    {
        var predicate = selector.Compile();
        foreach (var podcast in _podcasts.Values.Where(predicate).Select(Clone))
        {
            yield return podcast;
        }

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<TProjection> GetAllBy<TProjection>(
        Expression<Func<Podcast, bool>> selector,
        Expression<Func<Podcast, TProjection>> projection)
    {
        var predicate = selector.Compile();
        var project = projection.Compile();
        foreach (var podcast in _podcasts.Values.Where(predicate))
        {
            yield return project(podcast);
        }

        await Task.CompletedTask;
    }

    public Podcast GetStored(Guid podcastId) =>
        Clone(_podcasts[podcastId]);

    private static Podcast Clone(Podcast podcast) =>
        new()
        {
            Id = podcast.Id,
            Name = podcast.Name,
            SpotifyId = podcast.SpotifyId,
            AppleId = podcast.AppleId,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            ReleaseAuthority = podcast.ReleaseAuthority,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            MinimumDuration = podcast.MinimumDuration,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive,
            YouTubeChannelSearchForbidden = podcast.YouTubeChannelSearchForbidden,
            LastIndexed = podcast.LastIndexed,
            LatestReleased = podcast.LatestReleased,
            Removed = podcast.Removed
        };
}
