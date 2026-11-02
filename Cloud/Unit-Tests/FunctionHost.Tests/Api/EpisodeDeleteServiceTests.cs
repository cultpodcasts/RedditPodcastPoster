using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using Azure.Search.Documents;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api;

public class EpisodeDeleteServiceTests
{
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName = "Delete returns NotFound when episode or podcast missing")]
    public async Task Delete_returns_not_found_when_missing()
    {
        // Arrange
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound));

        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.NotFound);
    }

    [Fact(DisplayName = "Delete returns AlreadySocial when episode is tweeted")]
    public async Task Delete_returns_already_social_when_tweeted()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId, OldTweeted = true };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = _mocker.GetMock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>())).Throws(new InvalidOperationException("delete"));
        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.AlreadySocial);
        result.Tweeted.Should().BeTrue();
        episodeRepo.Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName = "Delete allows removal when episode was Reddit-posted but not tweeted")]
    public async Task Delete_allows_when_posted_but_not_tweeted()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId, Posted = true, OldTweeted = false };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = _mocker.GetMock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.Delete(podcastId, episodeId)).Returns(Task.CompletedTask);

        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));

        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        episodeRepo.Verify(r => r.Delete(podcastId, episodeId), Times.Once);
    }

    [Fact(DisplayName = "Delete persists removal and does not require social posters")]
    public async Task Delete_success_deletes_episode()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = _mocker.GetMock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.Delete(podcastId, episodeId)).Returns(Task.CompletedTask);

        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));

        var service = CreateService();

        // Act
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        episodeRepo.Verify(r => r.Delete(podcastId, episodeId), Times.Once);
    }

    private EpisodeDeleteService CreateService()
    {
        return new EpisodeDeleteService(
            _mocker.Get<IPodcastEpisodeResolver>(),
            _mocker.Get<IEpisodeRepository>(),
            new EpisodeSearchIndexCleanup(
                CreateUninitializedSearchClient(),
                NullLogger<EpisodeSearchIndexCleanup>.Instance),
            _mocker.Get<IShortnerService>(),
            NullLogger<EpisodeDeleteService>.Instance);
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
