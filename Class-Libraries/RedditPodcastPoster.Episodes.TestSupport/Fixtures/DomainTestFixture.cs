using AutoFixture;
using AutoFixture.Dsl;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Shared AutoFixture wrapper for episode-domain business-rule tests.
/// Provides sensible defaults and factory methods for platform catalogue specimens.
/// </summary>
public sealed class DomainTestFixture
{
    private readonly Fixture _fixture;

    public DomainTestFixture()
    {
        _fixture = new Fixture();
        CustomizeFixture();
    }

    public IFixture Auto => _fixture;

    public IPostprocessComposer<T> Build<T>() => _fixture.Build<T>();

    public T Create<T>() => _fixture.Create<T>();

    public IPostprocessComposer<Episode> BuildEpisode() =>
        _fixture.Build<Episode>()
            .FromFactory(() => new Episode
            {
                Id = Guid.NewGuid(),
                Urls = new ServiceUrls(),
                Subjects = [],
                SpotifyId = string.Empty,
                YouTubeId = string.Empty
            });

    public Episode CreateEpisode(Action<Episode> configure)
    {
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Urls = new ServiceUrls(),
            Subjects = [],
            SpotifyId = string.Empty,
            YouTubeId = string.Empty
        };
        configure(episode);
        return episode;
    }

    public IPostprocessComposer<Podcast> BuildPodcast() =>
        _fixture.Build<Podcast>()
            .FromFactory(() => new Podcast
            {
                Id = Guid.NewGuid(),
                Name = "Test Podcast",
                SpotifyId = string.Empty,
                YouTubeChannelId = string.Empty,
                YouTubePlaylistId = string.Empty
            });

    public Podcast StandardPodcast(Guid? id = null, string name = "Test Podcast") =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name
        };

    public Podcast SpotifyPrimaryPodcast(string spotifyShowId, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Spotify-primary podcast",
            SpotifyId = spotifyShowId,
            ReleaseAuthority = Service.Spotify
        };

    public Podcast YouTubeFirstPodcast(
        string channelId,
        long youTubePublicationOffsetTicks,
        string? spotifyShowId = null,
        Guid? id = null) =>
        new()
        {
            Id = id ?? PodcastFixtures.CultsToConsciousnessPodcastId,
            Name = "YouTube-first podcast",
            ReleaseAuthority = Service.YouTube,
            YouTubeChannelId = channelId,
            YouTubePublicationOffset = youTubePublicationOffsetTicks,
            SpotifyId = spotifyShowId ?? string.Empty
        };

    public Podcast CultsToConsciousnessPodcast() =>
        YouTubeFirstPodcast(
            PodcastFixtures.CultsToConsciousnessChannelId,
            PodcastFixtures.CultsToConsciousnessYouTubePublicationOffsetTicks,
            PodcastFixtures.CultsToConsciousnessSpotifyShowId,
            PodcastFixtures.CultsToConsciousnessPodcastId);

    public Episode SubmittedViaSpotifyUrlOnly(
        Uri spotifyUrl,
        string title = "Reddit post title",
        DateTime? release = null,
        Guid? podcastId = null,
        Guid? episodeId = null) =>
        new()
        {
            Id = episodeId ?? Guid.NewGuid(),
            PodcastId = podcastId ?? EpisodeFixtures.DefaultPodcastId,
            Title = title,
            Release = release ?? DateTime.UtcNow.Date,
            SpotifyId = string.Empty,
            Urls = new ServiceUrls { Spotify = spotifyUrl }
        };

    public Episode FromSpotifyCatalogue(
        string spotifyId,
        string title,
        Uri spotifyUrl,
        DateTime release,
        TimeSpan length,
        string description = "Catalogue description") =>
        Episode.FromSpotify(
            spotifyId,
            title,
            description,
            length,
            false,
            release,
            spotifyUrl,
            null);

    public Episode FromYouTubeVideo(
        string youTubeId,
        string title,
        DateTime release,
        TimeSpan length,
        string description = "YouTube description") =>
        Episode.FromYouTube(
            youTubeId,
            title,
            description,
            length,
            false,
            release,
            new Uri($"https://www.youtube.com/watch?v={youTubeId}"),
            null);

    public Episode FromAppleEpisode(
        long appleId,
        string title,
        DateTime release,
        TimeSpan length,
        string description = "Apple description") =>
        Episode.FromApple(
            appleId,
            title,
            description,
            length,
            false,
            release,
            new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}"),
            null);

    private void CustomizeFixture()
    {
        _fixture.Register(() => new Uri("https://example.com/test"));
    }
}
