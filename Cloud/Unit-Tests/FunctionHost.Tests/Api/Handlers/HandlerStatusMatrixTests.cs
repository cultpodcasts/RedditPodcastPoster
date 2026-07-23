using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Handlers;
using Api.Handlers.Episodes;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.Terms;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.People;
using Api.Services.Podcasts;
using Api.Services.Public;
using Api.Services.Subjects;
using Api.Services.SubmitUrl;
using Api.Services.Terms;
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

    // ----- DeleteEpisodeHandler -----

    [Fact(DisplayName = "DeleteEpisodeHandler maps Deleted to 200")]
    public async Task DeleteEpisodeHandler_deleted_returns_200()
    {
        var service = new Mock<IEpisodeDeleteService>();
        service.Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted));

        var handler = new DeleteEpisodeHandler(service.Object, NullLogger<DeleteEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");
        var wrapper = new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid());

        var result = await handler.Handle(new HandlerContext(req.Object, null), wrapper, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DeleteEpisodeHandler maps NotFound to 404")]
    public async Task DeleteEpisodeHandler_not_found_returns_404()
    {
        var service = new Mock<IEpisodeDeleteService>();
        service.Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound));

        var handler = new DeleteEpisodeHandler(service.Object, NullLogger<DeleteEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DeleteEpisodeHandler maps PodcastConflict to 409")]
    public async Task DeleteEpisodeHandler_podcast_conflict_returns_409()
    {
        var service = new Mock<IEpisodeDeleteService>();
        service.Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.PodcastConflict));

        var handler = new DeleteEpisodeHandler(service.Object, NullLogger<DeleteEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "DeleteEpisodeHandler maps AlreadySocial to 400 with body")]
    public async Task DeleteEpisodeHandler_already_social_returns_400()
    {
        var service = new Mock<IEpisodeDeleteService>();
        service.Setup(s => s.DeleteAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EpisodeDeleteResult(EpisodeDeleteStatus.AlreadySocial, Posted: true, Tweeted: false));

        var handler = new DeleteEpisodeHandler(service.Object, NullLogger<DeleteEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("DELETE");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    // ----- PostTermsHandler -----

    [Fact(DisplayName = "PostTermsHandler maps Ok to 200")]
    public async Task PostTermsHandler_ok_returns_200()
    {
        var service = new Mock<ITermsSubmitService>();
        service.Setup(s => s.SubmitAsync(It.IsAny<TermSubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TermsSubmitResult(TermsSubmitStatus.Ok));

        var handler = new PostTermsHandler(service.Object, NullLogger<PostTermsHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TermSubmitRequest { Term = "cult" },
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "PostTermsHandler maps Conflict to 409")]
    public async Task PostTermsHandler_conflict_returns_409()
    {
        var service = new Mock<ITermsSubmitService>();
        service.Setup(s => s.SubmitAsync(It.IsAny<TermSubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TermsSubmitResult(TermsSubmitStatus.Conflict));

        var handler = new PostTermsHandler(service.Object, NullLogger<PostTermsHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TermSubmitRequest { Term = "cult" },
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "PostTermsHandler maps Failed to 500")]
    public async Task PostTermsHandler_failed_returns_500()
    {
        var service = new Mock<ITermsSubmitService>();
        service.Setup(s => s.SubmitAsync(It.IsAny<TermSubmitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TermsSubmitResult(TermsSubmitStatus.Failed));

        var handler = new PostTermsHandler(service.Object, NullLogger<PostTermsHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new TermSubmitRequest { Term = "cult" },
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ----- PostSubmitUrlHandler -----

    [Fact(DisplayName = "PostSubmitUrlHandler maps PodcastNotFound to 404")]
    public async Task PostSubmitUrlHandler_podcast_not_found_returns_404()
    {
        var service = new Mock<ISubmitUrlService>();
        service.Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlResult(SubmitUrlStatus.PodcastNotFound, Message: "missing"));

        var handler = new PostSubmitUrlHandler(service.Object, NullLogger<PostSubmitUrlHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri("https://example.com/ep") },
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PostSubmitUrlHandler maps Failed to 500")]
    public async Task PostSubmitUrlHandler_failed_returns_500()
    {
        var service = new Mock<ISubmitUrlService>();
        service.Setup(s => s.SubmitAsync(It.IsAny<SubmitUrlRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubmitUrlResult(SubmitUrlStatus.Failed, Message: "boom"));

        var handler = new PostSubmitUrlHandler(service.Object, NullLogger<PostSubmitUrlHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubmitUrlRequest { Url = new Uri("https://example.com/ep") },
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ----- GetPublicEpisodeHandler -----

    [Fact(DisplayName = "GetPublicEpisodeHandler maps NotFound to 404")]
    public async Task GetPublicEpisodeHandler_not_found_returns_404()
    {
        var service = new Mock<IPublicEpisodeGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicEpisodeGetResult(PublicEpisodeGetStatus.NotFound));

        var handler = new GetPublicEpisodeHandler(service.Object, NullLogger<GetPublicEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetPublicEpisodeHandler maps Failed to 500 with error JSON body")]
    public async Task GetPublicEpisodeHandler_failed_returns_500_with_error_body()
    {
        var service = new Mock<IPublicEpisodeGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicEpisodeGetResult(PublicEpisodeGetStatus.Failed));

        var handler = new GetPublicEpisodeHandler(service.Object, NullLogger<GetPublicEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PodcastEpisodeRequestWrapper(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await ReadJsonBodyAsync(result);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
