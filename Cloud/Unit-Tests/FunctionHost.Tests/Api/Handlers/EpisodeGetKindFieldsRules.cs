using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Handlers.Public;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.Public;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>The GET-by-id episode bodies add kind fields; other episode bodies omit them.</summary>
public class EpisodeGetKindFieldsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private EpisodeGetResult _getResult = new(EpisodeGetStatus.EpisodeNotFound);
    private PublicEpisodeGetResult _publicResult = new(PublicEpisodeGetStatus.NotFound);

    public EpisodeGetKindFieldsRules()
    {
        _mocker.Use(NullLogger<GetEpisodeHandler>.Instance);
        _mocker.Use(NullLogger<GetPublicEpisodeHandler>.Instance);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(s => s.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _) => title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(s => s.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
        _mocker.GetMock<IEpisodeGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _getResult);
        _mocker.GetMock<IPublicEpisodeGetService>()
            .Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _publicResult);
    }

    private static async Task<JsonElement> ReadJsonBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        using var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
        return doc.RootElement.Clone();
    }

    private PlayableKind CreateNewsKind(string parentName, Guid parentId) =>
        new("NewsReport", parentId, parentName, parentName,
            $"/news/{Uri.EscapeDataString(parentName)}/{_fixture.CreateYouTubeId()}");

    [Fact(DisplayName =
        "Plain English rule: when the curator get carries the item's kind, then the 200 body adds contentKind, parentId, parentName, slug and path next to the unchanged podcast fields, because the Review page links every kind from GET /episode/{id}.")]
    public async Task curator_get_body_adds_kind_fields()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Name = _fixture.CreateTitle());
        var episode = _fixture.CreateStoredEpisode(podcast);
        var kind = CreateNewsKind(podcast.Name, podcast.Id);
        _getResult = new EpisodeGetResult(EpisodeGetStatus.Ok, episode, podcast, Array.Empty<Subject>(), kind);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var sut = _mocker.CreateInstance<GetEpisodeHandler>();

        // Act
        var result = await sut.Handle(new HandlerContext(req.Object, null), new PodcastEpisodeRequestWrapper(episode.Id),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("podcastName").GetString().Should().Be(podcast.Name);
        body.GetProperty("podcastId").GetGuid().Should().Be(podcast.Id);
        body.GetProperty("contentKind").GetString().Should().Be(kind.ContentKind);
        body.GetProperty("parentId").GetGuid().Should().Be(podcast.Id);
        body.GetProperty("parentName").GetString().Should().Be(podcast.Name);
        body.GetProperty("slug").GetString().Should().Be(podcast.Name);
        body.GetProperty("path").GetString().Should().Be(kind.Path);
    }

    [Fact(DisplayName =
        "Plain English rule: when the public get is for a film, then the body has contentKind Film, slug and path but no parentId or parentName, because a film has no parent and null kind fields are omitted.")]
    public async Task public_get_body_for_film_omits_parent_fields()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Name = _fixture.CreateTitle());
        var episode = _fixture.CreateStoredEpisode(podcast);
        var kind = new PlayableKind("Film", null, null, podcast.Name, $"/film/{Uri.EscapeDataString(podcast.Name)}/{_fixture.CreateYouTubeId()}");
        _publicResult = new PublicEpisodeGetResult(PublicEpisodeGetStatus.Ok, episode, podcast, kind);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var sut = _mocker.CreateInstance<GetPublicEpisodeHandler>();

        // Act
        var result = await sut.Handle(new HandlerContext(req.Object, null), new PodcastEpisodeRequestWrapper(episode.Id),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("contentKind").GetString().Should().Be("Film");
        body.GetProperty("slug").GetString().Should().Be(podcast.Name);
        body.GetProperty("path").GetString().Should().Be(kind.Path);
        body.TryGetProperty("parentId", out _).Should().BeFalse();
        body.TryGetProperty("parentName", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Plain English rule: when an episode body is built without a kind, then the kind fields are omitted, because only the GET-by-id endpoints add them and other episode responses keep their shape.")]
    public async Task body_without_kind_omits_kind_fields()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        _publicResult = new PublicEpisodeGetResult(PublicEpisodeGetStatus.Ok, episode, podcast);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");
        var sut = _mocker.CreateInstance<GetPublicEpisodeHandler>();

        // Act
        var result = await sut.Handle(new HandlerContext(req.Object, null), new PodcastEpisodeRequestWrapper(episode.Id),
            CancellationToken.None);

        // Assert
        var body = await ReadJsonBodyAsync(result);
        foreach (var field in new[] { "contentKind", "parentId", "parentName", "slug", "path" })
        {
            body.TryGetProperty(field, out _).Should().BeFalse(field);
        }
    }
}
