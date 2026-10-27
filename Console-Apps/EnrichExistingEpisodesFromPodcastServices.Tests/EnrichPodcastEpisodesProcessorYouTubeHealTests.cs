using EnrichExistingEpisodesFromPodcastServices;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace EnrichExistingEpisodesFromPodcastServices.Tests;

public class EnrichPodcastEpisodesProcessorYouTubeHealTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SaveCallRecorder _saves = new();
    private readonly InMemoryEpisodeRepository _episodes;
    private readonly Podcast _podcast;

    public EnrichPodcastEpisodesProcessorYouTubeHealTests()
    {
        _podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = "UCsynthetic000000000000";
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        _episodes = new InMemoryEpisodeRepository(_saves);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetPodcast(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => id == _podcast.Id ? _podcast : null);
    }

    [Fact(DisplayName =
        "Enrich existing episodes: when an episode has services.youtube.url but no ids.youtube, then the id is set " +
        "from the url and the episode is saved, because an unsaved heal never reaches Cosmos or search.")]
    public async Task url_without_id_is_healed_and_saved()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        episode.Services![ServiceKeys.YouTube] = new ServiceLink { Url = _fixture.DefaultYouTubeUrl(videoId) };
        EpisodeServicePresence.NestedYouTubeId(episode).Should().BeNull();
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<EnrichPodcastEpisodesProcessor>();

        // Act
        await sut.Run(new EnrichPodcastEpisodesRequest { PodcastId = _podcast.Id });

        // Assert
        _saves.EpisodeCalls.Should().ContainSingle()
            .Which.EpisodeIds.Should().Equal(episode.Id);
        var saved = await _episodes.GetEpisode(_podcast.Id, episode.Id);
        EpisodeServicePresence.NestedYouTubeId(saved!).Should().Be(videoId);
    }

    [Fact(DisplayName =
        "Enrich existing episodes: when an episode has ids.youtube but no services.youtube.url, then the url is " +
        "built from the id and the episode is saved.")]
    public async Task id_without_url_is_healed_and_saved()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        EpisodeServicePresence.SetYouTubeIdentity(episode, videoId);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<EnrichPodcastEpisodesProcessor>();

        // Act
        await sut.Run(new EnrichPodcastEpisodesRequest { PodcastId = _podcast.Id });

        // Assert
        _saves.EpisodeCalls.Should().ContainSingle()
            .Which.EpisodeIds.Should().Equal(episode.Id);
        var saved = await _episodes.GetEpisode(_podcast.Id, episode.Id);
        EpisodeServicePresence.TryGetUrl(saved!, ServiceKeys.YouTube)!.ToString().Should().Contain(videoId);
        EpisodeServicePresence.NestedYouTubeId(saved!).Should().Be(videoId);
    }
}
