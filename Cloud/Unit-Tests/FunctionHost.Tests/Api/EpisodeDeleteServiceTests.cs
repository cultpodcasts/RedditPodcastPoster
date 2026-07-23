using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    [Fact(DisplayName = "Delete returns PodcastConflict when podcast name resolves ambiguously")]
    public async Task Delete_returns_conflict_on_podcast_ambiguity()
    {
        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastConflict));

        var service = CreateService(resolver.Object);
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper("Ambiguous Show", Guid.NewGuid()),
            CancellationToken.None);

        result.Status.Should().Be(EpisodeDeleteStatus.PodcastConflict);
    }

    [Fact(DisplayName = "Delete returns NotFound when episode or podcast missing")]
    public async Task Delete_returns_not_found_when_missing()
    {
        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound));

        var service = CreateService(resolver.Object);
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(Guid.NewGuid()),
            CancellationToken.None);

        result.Status.Should().Be(EpisodeDeleteStatus.NotFound);
    }

    [Fact(DisplayName = "Delete returns AlreadySocial when episode already posted or tweeted")]
    public async Task Delete_returns_already_social_when_tweeted()
    {
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId, Tweeted = true };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = new Mock<IEpisodeRepository>(MockBehavior.Strict);
        var service = CreateService(resolver.Object, episodeRepo.Object);

        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            CancellationToken.None);

        result.Status.Should().Be(EpisodeDeleteStatus.AlreadySocial);
        result.Tweeted.Should().BeTrue();
        episodeRepo.Verify(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName = "Delete persists removal and does not require social posters")]
    public async Task Delete_success_deletes_episode()
    {
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId };
        var podcast = new Podcast { Id = podcastId, Name = "Show" };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var episodeRepo = new Mock<IEpisodeRepository>();
        episodeRepo.Setup(r => r.Delete(podcastId, episodeId)).Returns(Task.CompletedTask);

        var shortner = new Mock<IShortnerService>();
        shortner.Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(new RedditPodcastPoster.Cloudflare.Models.DeleteResult(true));

        var service = CreateService(resolver.Object, episodeRepo.Object, shortner.Object);
        var result = await service.DeleteAsync(
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            CancellationToken.None);

        result.Status.Should().Be(EpisodeDeleteStatus.Deleted);
        episodeRepo.Verify(r => r.Delete(podcastId, episodeId), Times.Once);
    }

    private static EpisodeDeleteService CreateService(
        IPodcastEpisodeResolver resolver,
        IEpisodeRepository? episodeRepository = null,
        IShortnerService? shortner = null)
    {
        return new EpisodeDeleteService(
            resolver,
            episodeRepository ?? Mock.Of<IEpisodeRepository>(),
            new EpisodeSearchIndexCleanup(
                CreateUninitializedSearchClient(),
                NullLogger<EpisodeSearchIndexCleanup>.Instance),
            shortner ?? Mock.Of<IShortnerService>(),
            NullLogger<EpisodeDeleteService>.Instance);
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
