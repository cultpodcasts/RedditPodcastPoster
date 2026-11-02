using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using Azure.Search.Documents;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api;

public class EpisodeDeleteServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastEpisodeResolverResponse _resolved =
        new(null, null, PodcastEpisodeResolveState.PodcastNotFound);

    public EpisodeDeleteServiceTests()
    {
        _mocker.Use(new EpisodeSearchIndexCleanup(
            CreateUninitializedSearchClient(),
            NullLogger<EpisodeSearchIndexCleanup>.Instance));
        _mocker.Use(NullLogger<EpisodeDeleteService>.Instance);
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(() => _resolved);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName = "Delete returns NotFound when episode or podcast missing")]
    public async Task Delete_returns_not_found_when_missing()
    {
        // Arrange
        _resolved = new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound);
        var sut = _mocker.CreateInstance<EpisodeDeleteService>();

        // Act
        var result = await sut.DeleteAsync(
            new PodcastEpisodeRequestWrapper(_fixture.CreateGuid()),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.NotFound);
    }

    [Fact(DisplayName = "Delete returns AlreadySocial when episode is tweeted")]
    public async Task Delete_returns_already_social_when_tweeted()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.OldTweeted = true);
        _resolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        var sut = _mocker.CreateInstance<EpisodeDeleteService>();

        // Act
        var result = await sut.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcast.Id, episode.Id),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.AlreadySocial);
        result.Tweeted.Should().BeTrue();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName = "Delete allows removal when episode was Reddit-posted but not tweeted")]
    public async Task Delete_allows_when_posted_but_not_tweeted()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Posted = true;
            e.OldTweeted = false;
        });
        _resolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        var sut = _mocker.CreateInstance<EpisodeDeleteService>();

        // Act
        var result = await sut.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcast.Id, episode.Id),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(r => r.Delete(podcast.Id, episode.Id), Times.Once);
    }

    [Fact(DisplayName = "Delete persists removal and does not require social posters")]
    public async Task Delete_success_deletes_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        _resolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        var sut = _mocker.CreateInstance<EpisodeDeleteService>();

        // Act
        var result = await sut.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcast.Id, episode.Id),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(r => r.Delete(podcast.Id, episode.Id), Times.Once);
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
