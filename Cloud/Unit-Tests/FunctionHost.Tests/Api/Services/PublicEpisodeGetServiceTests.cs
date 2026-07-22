using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Models;
using Api.Resolvers;
using Api.Services.Public;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services;

public class PublicEpisodeGetServiceTests
{
    [Fact(DisplayName = "Get returns NotFound when episode missing or removed")]
    public async Task Get_returns_not_found_when_episode_missing()
    {
        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound));

        var service = new PublicEpisodeGetService(resolver.Object, NullLogger<PublicEpisodeGetService>.Instance);
        var result = await service.GetAsync(new PodcastEpisodeRequestWrapper(Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(PublicEpisodeGetStatus.NotFound);
    }

    [Fact(DisplayName = "Get returns Ok with domain episode and podcast")]
    public async Task Get_returns_ok_with_domain_entities()
    {
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode { Id = episodeId, PodcastId = podcastId, Title = "Ep", Removed = false };
        var podcast = new Podcast { Id = podcastId, Name = "Show", Removed = false };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var service = new PublicEpisodeGetService(resolver.Object, NullLogger<PublicEpisodeGetService>.Instance);
        var result = await service.GetAsync(new PodcastEpisodeRequestWrapper(podcastId, episodeId), CancellationToken.None);

        result.Status.Should().Be(PublicEpisodeGetStatus.Ok);
        result.Episode.Should().BeSameAs(episode);
        result.Podcast.Should().BeSameAs(podcast);
    }

    [Fact(DisplayName = "Get returns NotFound when podcast removed")]
    public async Task Get_returns_not_found_when_podcast_removed()
    {
        var episode = new Episode { Id = Guid.NewGuid(), Removed = false };
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Gone", Removed = true };

        var resolver = new Mock<IPodcastEpisodeResolver>();
        resolver.Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved));

        var service = new PublicEpisodeGetService(resolver.Object, NullLogger<PublicEpisodeGetService>.Instance);
        var result = await service.GetAsync(new PodcastEpisodeRequestWrapper(episode.Id), CancellationToken.None);

        result.Status.Should().Be(PublicEpisodeGetStatus.NotFound);
    }
}
