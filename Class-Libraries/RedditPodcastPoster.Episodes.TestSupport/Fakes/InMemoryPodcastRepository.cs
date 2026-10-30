using System.Linq.Expressions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Episodes.TestSupport.Fakes;

public sealed class InMemoryPodcastRepository : IPodcastRepository
{
    private readonly Dictionary<Guid, Podcast> _podcasts = new();

    private readonly List<Podcast> _savedPodcasts = [];

    public IReadOnlyList<Podcast> SavedPodcasts => _savedPodcasts;

    /// <summary>
    /// Runs at the start of a point-read, so a test can cancel while that read is in flight.
    /// </summary>
    public Action? OnReading { get; set; }

    public void Seed(params Podcast[] podcasts)
    {
        foreach (var podcast in podcasts)
        {
            _podcasts[podcast.Id] = Clone(podcast);
        }
    }

    public Task<Podcast?> GetPodcast(Guid podcastId)
    {
        OnReading?.Invoke();
        return Task.FromResult(_podcasts.TryGetValue(podcastId, out var podcast) ? Clone(podcast) : null);
    }

    public Task Save(Podcast entity)
    {
        _podcasts[entity.Id] = Clone(entity);
        _savedPodcasts.Add(Clone(entity));
        return Task.CompletedTask;
    }

    public Task Delete(Guid podcastId)
    {
        _podcasts.Remove(podcastId);
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
        var predicate = CosmosLinqInMemoryRewriter.ForInMemory(selector).Compile();
        var match = _podcasts.Values.FirstOrDefault(predicate);
        return Task.FromResult(match is null ? null : Clone(match));
    }

    public async IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector)
    {
        var predicate = CosmosLinqInMemoryRewriter.ForInMemory(selector).Compile();
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
        var predicate = CosmosLinqInMemoryRewriter.ForInMemory(selector).Compile();
        var project = CosmosLinqInMemoryRewriter.ForInMemory(projection).Compile();
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
            YouTubePlaylistIdHistory = podcast.YouTubePlaylistIdHistory?
                .Select(x => new YouTubePlaylistIdHistoryEntry { Id = x.Id, ReplacedAt = x.ReplacedAt })
                .ToList(),
            YouTubePlaylistOrder = podcast.YouTubePlaylistOrder,
            YouTubeIndexingHints = podcast.YouTubeIndexingHints?.Copy(),
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            ReleaseAuthority = podcast.ReleaseAuthority,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            AlwaysPromoteAsHero = podcast.AlwaysPromoteAsHero,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            Bundles = podcast.Bundles,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes,
            SpotifyMarket = podcast.SpotifyMarket,
            PrimaryPostService = podcast.PrimaryPostService,
            MinimumDuration = podcast.MinimumDuration,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            SpotifyEpisodeCount = podcast.SpotifyEpisodeCount,
            AppleEpisodeCount = podcast.AppleEpisodeCount,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive,
            YouTubeChannelSearchForbidden = podcast.YouTubeChannelSearchForbidden,
            LastIndexed = podcast.LastIndexed,
            LatestReleased = podcast.LatestReleased,
            Removed = podcast.Removed
        };
}
