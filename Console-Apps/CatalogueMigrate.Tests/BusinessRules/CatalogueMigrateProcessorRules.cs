using CatalogueMigrate;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Xunit;

namespace CatalogueMigrate.Tests.BusinessRules;

public class CatalogueMigrateProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly AutoMocker _mocker;

    public CatalogueMigrateProcessorRules()
    {
        _mocker = new AutoMocker();
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use(NullLogger<CatalogueMigrateProcessor>.Instance);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when --apply is set, then the tool exits 2 without planning, " +
        "because --apply is always refused and writes are not implemented.")]
    public async Task apply_is_refused()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest
        {
            Kind = SubmitClassification.NewsReport,
            Apply = true
        });

        // Assert
        result.ExitCode.Should().Be(2);
        result.PlannedCount.Should().Be(0);
        result.SearchDocumentCount.Should().Be(0);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when Film has no --podcast-id, then the tool exits 1, " +
        "because stored identify cannot flag Film.")]
    public async Task film_requires_podcast_id()
    {
        // Arrange
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.Film });

        // Assert
        result.ExitCode.Should().Be(1);
        result.PlannedCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a stored BBC news URL is scanned as NewsReport, then dry-run plans one move " +
        "and does not write.")]
    public async Task news_scan_plans_bbc_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(1);
        result.SearchDocumentCount.Should().Be(1);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a podcast has a BBC news episode and a Spotify episode, then News dry-run " +
        "does not increment PlannedCount, because mixed shows are not an accepted station move.")]
    public async Task mixed_bbc_news_and_spotify_is_not_planned()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var newsEpisode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        var spotifyEpisode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            spotifyEpisode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(newsEpisode, spotifyEpisode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(0);
        result.SearchDocumentCount.Should().Be(0);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when --podcast-id News is mixed BBC news and Spotify, then dry-run still does not plan, " +
        "because targeted News does not skip mixed-show identify.")]
    public async Task podcast_id_news_mixed_is_not_planned()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var newsEpisode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        var spotifyEpisode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            spotifyEpisode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(newsEpisode, spotifyEpisode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest
        {
            Kind = SubmitClassification.NewsReport,
            PodcastId = podcast.Id
        });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(0);
        result.SearchDocumentCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when Film is given --podcast-id and one episode, then dry-run plans a Film move.")]
    public async Task film_podcast_id_plans_one_off()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest
        {
            Kind = SubmitClassification.Film,
            PodcastId = podcast.Id
        });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(1);
        result.SearchDocumentCount.Should().Be(1);
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a YouTube-only four-letter publisher is scanned as NewsReport, then dry-run plans one move " +
        "and does not write, because S-008 heuristics can discover news stations without BBC news URLs.")]
    public async Task news_scan_plans_youtube_only_four_letter()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = CreateFourLetterName();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(1);
        result.SearchDocumentCount.Should().Be(1);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a YouTube-only publisher named with the word News is scanned as NewsReport, then dry-run plans one move " +
        "and does not write, because US stations often brand with News rather than four call letters.")]
    public async Task news_scan_plans_youtube_only_news_word_name()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = $"{_fixture.CreateTitle()} News";
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(1);
        result.SearchDocumentCount.Should().Be(1);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when two stored iPlayer episode URLs exist, then TvShowEpisode dry-run without --podcast-id " +
        "plans one move, because stored identify can flag a series.")]
    public async Task tv_scan_plans_iplayer_series()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(first, second);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.TvShowEpisode });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(1);
        result.SearchDocumentCount.Should().Be(2);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when two stored iPlayer episode URLs exist on a publisher that also has a Spotify id, " +
        "then TvShowEpisode dry-run plans zero, because publisher Spotify blocks stored TV identify.")]
    public async Task tv_scan_does_not_plan_iplayer_when_publisher_has_spotify()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(first, second);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.TvShowEpisode });

        // Assert
        result.ExitCode.Should().Be(0);
        result.PlannedCount.Should().Be(0);
        result.SearchDocumentCount.Should().Be(0);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate: when a publisher is a curator TV programme, then TvShowEpisode dry-run plans one move " +
        "and NewsReport dry-run plans zero, because those rows are a TV show with a canonical episode page, not a news desk.")]
    public async Task tv_scan_plans_youtube_tv_publisher_and_news_scan_skips_it()
    {
        // Arrange
        var name = CatalogueTvShowCanonicalNames.PublisherNames.First();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = name;
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateProcessor>();

        // Act
        var tv = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.TvShowEpisode });
        var news = await sut.Run(new CatalogueMigrateRequest { Kind = SubmitClassification.NewsReport });

        // Assert
        tv.ExitCode.Should().Be(0);
        tv.PlannedCount.Should().Be(1);
        tv.SearchDocumentCount.Should().Be(1);
        news.ExitCode.Should().Be(0);
        news.PlannedCount.Should().Be(0);
        news.SearchDocumentCount.Should().Be(0);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    private string CreateFourLetterName()
    {
        var seed = _fixture.Create<int>() & int.MaxValue;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var name = string.Create(4, seed + attempt, static (span, value) =>
            {
                var n = value;
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[Math.Abs(n) % 26];
                    n = HashCode.Combine(n, i);
                }
            });
            if (!CatalogueTvShowCanonicalNames.IsPublisherName(name))
            {
                return name;
            }
        }

        return "WXYZ";
    }
}
