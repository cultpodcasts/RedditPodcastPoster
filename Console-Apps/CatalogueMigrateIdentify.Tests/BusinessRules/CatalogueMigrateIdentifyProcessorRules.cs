using CatalogueMigrateIdentify;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace CatalogueMigrateIdentify.Tests.BusinessRules;

public class CatalogueMigrateIdentifyProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly AutoMocker _mocker;

    public CatalogueMigrateIdentifyProcessorRules()
    {
        _mocker = new AutoMocker();
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use(NullLogger<CatalogueMigrateIdentifyProcessor>.Instance);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when --apply is set, then the tool exits 2 without scanning, " +
        "because apply is GATE 5 only.")]
    public async Task apply_is_refused_without_scanning()
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
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest { Apply = true });

        // Assert
        result.ExitCode.Should().Be(2);
        result.CandidateCount.Should().Be(0);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a stored episode URL is a BBC news page, then dry-run counts one candidate " +
        "and does not write, because identify is report-only.")]
    public async Task stored_bbc_news_url_is_a_dry_run_candidate()
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
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(1);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when episode URLs are Spotify only, then dry-run counts no candidates, " +
        "because podcast-service rows stay Episode.")]
    public async Task stored_spotify_is_not_a_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when the podcast is removed, then it is skipped, " +
        "because removed publishers are not migrate candidates.")]
    public async Task removed_podcast_is_skipped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when the only news URL is on a removed episode, then the podcast is not a candidate, " +
        "because identify ignores removed episodes.")]
    public async Task removed_episode_is_ignored()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Removed = true);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when --podcast-id is set, then only that podcast is scanned.")]
    public async Task podcast_id_limits_the_scan()
    {
        // Arrange
        var newsPodcast = _fixture.CreatePodcast();
        var otherPodcast = _fixture.CreatePodcast();
        var newsEpisode = _fixture.CreateStoredEpisode(newsPodcast);
        var otherEpisode = _fixture.CreateStoredEpisode(otherPodcast);
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        EpisodeServicePresence.Upsert(
            otherEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(newsPodcast, otherPodcast);
        _episodes.Seed(newsEpisode, otherEpisode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest { PodcastId = newsPodcast.Id });

        // Assert
        result.CandidateCount.Should().Be(1);
    }
}
