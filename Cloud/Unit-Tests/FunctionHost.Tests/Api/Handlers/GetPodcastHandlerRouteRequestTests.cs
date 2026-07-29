using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Handlers;
using Api.Handlers.Podcasts;
using Api.Models;
using Api.Services.Podcasts;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Handlers;

public class GetPodcastHandlerRouteRequestTests
{
    [Fact(DisplayName =
        "After PodcastGetSlash resolves podcastGuid/episodeGuid: GetPodcastHandler calls GetAsync with PodcastId (not PodcastName), because that is the request shape that must reach GetAsync.")]
    public async Task catch_all_guid_pair_handler_passes_podcast_id_to_get_async()
    {
        // Arrange
        var podcastId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var resolution = PodcastGetRouteResolver.ForCatchAll($"{podcastId:D}/{episodeId:D}");
        PodcastGetRequest? captured = null;
        var service = new Mock<IPodcastGetService>();
        service
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PodcastGetRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PodcastGetResult(
                PodcastGetStatus.Found,
                new Podcast { Id = podcastId, Name = "Show" }));
        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            resolution.HandlerRequest,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        resolution.InvokedFunction.Should().Be(PodcastGetFunction.PodcastGetSlash);
        captured.Should().NotBeNull();
        captured!.PodcastId.Should().Be(podcastId);
        captured.PodcastName.Should().BeNull();
        captured.ToString().Should().Be($"PodcastId: '{podcastId}'.");
        service.Verify(
            s => s.GetAsync(It.Is<PodcastGetRequest>(r => r.PodcastId == podcastId && r.PodcastName == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "After PodcastGetSlash resolves podcast-name?/episodeId: GetPodcastHandler calls GetAsync with PodcastName + EpisodeId.")]
    public async Task catch_all_question_mark_name_and_episode_handler_passes_name_lookup_to_get_async()
    {
        // Arrange
        const string podcastName = "Was I In A Cult?";
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var resolution = PodcastGetRouteResolver.ForCatchAll($"{podcastName}/{episodeId:D}");
        PodcastGetRequest? captured = null;
        var service = new Mock<IPodcastGetService>();
        service
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PodcastGetRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PodcastGetResult(
                PodcastGetStatus.Found,
                new Podcast { Id = podcastId, Name = podcastName }));
        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            resolution.HandlerRequest,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Should().NotBeNull();
        captured!.PodcastId.Should().BeNull();
        captured.PodcastName.Should().Be(podcastName);
        captured.EpisodeId.Should().Be(episodeId);
        service.Verify(
            s => s.GetAsync(
                It.Is<PodcastGetRequest>(r =>
                    r.PodcastName == podcastName && r.EpisodeId == episodeId && r.PodcastId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "After PodcastGetSlash resolves slash-containing podcast-name/episodeId: GetPodcastHandler calls GetAsync with PodcastName + EpisodeId.")]
    public async Task catch_all_slash_name_and_episode_handler_passes_name_lookup_to_get_async()
    {
        // Arrange
        const string podcastName = "True Crime Show w/ Guest Host";
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var resolution = PodcastGetRouteResolver.ForCatchAll($"{podcastName}/{episodeId:D}");
        PodcastGetRequest? captured = null;
        var service = new Mock<IPodcastGetService>();
        service
            .Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PodcastGetRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PodcastGetResult(
                PodcastGetStatus.Found,
                new Podcast { Id = podcastId, Name = podcastName }));
        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            resolution.HandlerRequest,
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        captured.Should().NotBeNull();
        captured!.PodcastId.Should().BeNull();
        captured.PodcastName.Should().Be(podcastName);
        captured.EpisodeId.Should().Be(episodeId);
    }
}
