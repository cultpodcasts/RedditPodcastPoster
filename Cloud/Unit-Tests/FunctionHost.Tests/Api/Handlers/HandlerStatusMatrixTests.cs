using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Handlers;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Subjects;
using Api.Models;
using Api.Services.People;
using Api.Services.Podcasts;
using Api.Services.Subjects;
using Xunit;
using Person = RedditPodcastPoster.Models.People.Person;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

/// <summary>
/// Thin-handler status-to-HTTP mapping matrix. Handlers are Controller -> Handler -> Service ->
/// Models.*Result; handlers switch on the service Status enum and call ToDto() on success. These
/// tests mock the service one layer below the handler and assert only on the HTTP outcome (status
/// code, and — for failure paths — the JSON "error" body), so they stay valid whether the failure
/// DTO is Api.Dtos.SubmitUrlResponse or Api.Dtos.ApiErrorResponse.
/// </summary>
public class HandlerStatusMatrixTests
{
    private static async Task<JsonElement> ReadJsonBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ----- GetPersonHandler -----

    [Fact(DisplayName = "GetPersonHandler maps Ok to 200")]
    public async Task GetPersonHandler_ok_returns_200()
    {
        var person = new Person("Ada Lovelace") { Id = Guid.NewGuid() };
        var service = new Mock<IPersonGetService>();
        service.Setup(s => s.GetAsync("Ada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.Ok, person));

        var handler = new GetPersonHandler(service.Object, NullLogger<GetPersonHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), "Ada", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetPersonHandler maps NotFound to 404")]
    public async Task GetPersonHandler_not_found_returns_404()
    {
        var service = new Mock<IPersonGetService>();
        service.Setup(s => s.GetAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.NotFound));

        var handler = new GetPersonHandler(service.Object, NullLogger<GetPersonHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), "missing", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetPersonHandler maps Failed to 500 with error JSON body")]
    public async Task GetPersonHandler_failed_returns_500_with_error_body()
    {
        var service = new Mock<IPersonGetService>();
        service.Setup(s => s.GetAsync("Ada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.Failed));

        var handler = new GetPersonHandler(service.Object, NullLogger<GetPersonHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), "Ada", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ----- GetPodcastHandler -----

    [Fact(DisplayName = "GetPodcastHandler maps Found to 200")]
    public async Task GetPodcastHandler_found_returns_200()
    {
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Show" };
        var service = new Mock<IPodcastGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.Found, podcast));

        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), new PodcastGetRequest(podcast.Id), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetPodcastHandler maps NotFound to 404 with error JSON body")]
    public async Task GetPodcastHandler_not_found_returns_404()
    {
        var service = new Mock<IPodcastGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.NotFound));

        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), new PodcastGetRequest(Guid.NewGuid()), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "GetPodcastHandler maps Conflict with ambiguous podcasts to 409 with candidate list body")]
    public async Task GetPodcastHandler_conflict_with_ambiguous_returns_409()
    {
        var ambiguous = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var service = new Mock<IPodcastGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.Conflict, AmbiguousPodcasts: ambiguous));

        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), new PodcastGetRequest("show", null), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonBodyAsync(result);
        body.GetArrayLength().Should().Be(ambiguous.Length);
    }

    [Fact(DisplayName = "GetPodcastHandler maps Failed to 500 with error JSON body")]
    public async Task GetPodcastHandler_failed_returns_500_with_error_body()
    {
        var service = new Mock<IPodcastGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastGetRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PodcastGetResult(PodcastGetStatus.Failed));

        var handler = new GetPodcastHandler(service.Object, NullLogger<GetPodcastHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), new PodcastGetRequest(Guid.NewGuid()), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ----- GetSubjectHandler -----

    [Fact(DisplayName = "GetSubjectHandler maps Ok to 200")]
    public async Task GetSubjectHandler_ok_returns_200()
    {
        var subject = new Subject("News");
        var service = new Mock<ISubjectGetService>();
        service.Setup(s => s.GetAsync("News", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectGetResult(SubjectGetStatus.Ok, subject));

        var handler = new GetSubjectHandler(service.Object, NullLogger<GetSubjectHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), "News", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetSubjectHandler maps NotFound to 404")]
    public async Task GetSubjectHandler_not_found_returns_404()
    {
        var service = new Mock<ISubjectGetService>();
        service.Setup(s => s.GetAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectGetResult(SubjectGetStatus.NotFound));

        var handler = new GetSubjectHandler(service.Object, NullLogger<GetSubjectHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), "missing", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetSubjectHandler maps Failed to 500 with error JSON body")]
    public async Task GetSubjectHandler_failed_returns_500_with_error_body()
    {
        var service = new Mock<ISubjectGetService>();
        service.Setup(s => s.GetAsync("News", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectGetResult(SubjectGetStatus.Failed));

        var handler = new GetSubjectHandler(service.Object, NullLogger<GetSubjectHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(new HandlerContext(req.Object, null), "News", CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
