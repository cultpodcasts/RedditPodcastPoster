using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Dtos.Mapping;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.Providers;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

public class GetEpisodeHandlerTests
{
    private static async Task<JsonElement> ReadJsonBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact(DisplayName =
        "Plain English rule: when episode get succeeds, then respond 200 with stable EpisodeDto keys including id, podcastId, podcastName, duration, and bluesky, because curators rely on a stable JSON contract.")]
    public async Task ok_returns_200_with_stable_episode_dto_keys()
    {
        // Arrange
        var episodeId = Guid.NewGuid();
        var podcastId = Guid.NewGuid();
        var episode = new Episode
        {
            Id = episodeId,
            PodcastId = podcastId,
            Title = "Episode title",
            Length = TimeSpan.FromMinutes(45),
            BlueskyPosted = true,
            Release = DateTime.UtcNow.AddDays(-1)
        };
        var podcast = new Podcast { Id = podcastId, Name = "Podcast name" };

        var getService = new Mock<IEpisodeGetService>();
        getService.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeGetResult(EpisodeGetStatus.Ok, episode, podcast, Array.Empty<Subject>()));

        var handler = new GetEpisodeHandler(getService.Object, CreateEpisodeDtoMapper(), NullLogger<GetEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("id").GetGuid().Should().Be(episodeId);
        body.GetProperty("podcastId").GetGuid().Should().Be(podcastId);
        body.GetProperty("podcastName").GetString().Should().Be("Podcast name");
        body.TryGetProperty("duration", out _).Should().BeTrue();
        body.GetProperty("bluesky").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName =
        "Plain English rule: when the episode is not found, then respond 404, because the client asked for a missing episode.")]
    public async Task episode_not_found_returns_404()
    {
        // Arrange
        var getService = new Mock<IEpisodeGetService>();
        getService.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeGetResult(EpisodeGetStatus.EpisodeNotFound));

        var handler = new GetEpisodeHandler(getService.Object, CreateEpisodeDtoMapper(), NullLogger<GetEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast is not found for a resolved episode, then respond 404, because the episode cannot be returned without its podcast.")]
    public async Task podcast_not_found_returns_404()
    {
        // Arrange
        var getService = new Mock<IEpisodeGetService>();
        getService.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeGetResult(EpisodeGetStatus.PodcastNotFound));

        var handler = new GetEpisodeHandler(getService.Object, CreateEpisodeDtoMapper(), NullLogger<GetEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static EpisodeDtoMapper CreateEpisodeDtoMapper()
    {
        var sanitiser = new Mock<ITextSanitiser>();
        sanitiser.Setup(s => s.SanitiseTitle(
                It.IsAny<string>(),
                It.IsAny<Regex?>(),
                It.IsAny<string[]>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _) => title);
        sanitiser.Setup(s => s.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);

        var personService = new Mock<IPersonService>();
        personService.Setup(p => p.MatchEpisode(It.IsAny<Episode>())).ReturnsAsync([]);

        var subjectsProvider = new Mock<ICachedSubjectProvider>();
        subjectsProvider.Setup(p => p.GetAll()).Returns(EmptyAsync<Subject>());

        return new EpisodeDtoMapper(sanitiser.Object, personService.Object, subjectsProvider.Object);
    }

    private static async IAsyncEnumerable<T> EmptyAsync<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
