using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using Moq.AutoMock;
using Api.Handlers;
using Api.Handlers.People;
using Api.Handlers.Subjects;
using Api.Models;
using Api.Services.People;
using Api.Services.Subjects;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;
using Person = RedditPodcastPoster.Models.People.Person;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

public class CreateAcknowledgementHandlerTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static async Task<string> ReadBodyAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    [Fact(DisplayName =
        "Plain English rule: when subject create is accepted, then respond 202 with an empty body, because a command acknowledges the write and does not return the subject read model.")]
    public async Task subject_create_accepted_returns_202_with_empty_body()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var created = new Subject(name) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ISubjectCreateService>()
            .Setup(s => s.CreateAsync(It.IsAny<SubjectChangeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectCreateResult(SubjectCreateStatus.Accepted, created));
        var handler = _mocker.CreateInstance<PutSubjectHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new SubjectChangeRequest { Name = name },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when person create is accepted, then respond 202 with an empty body, because a command acknowledges the write and does not return the person read model.")]
    public async Task person_create_accepted_returns_202_with_empty_body()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var created = new Person(name) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IPersonCreateService>()
            .Setup(s => s.CreateAsync(It.IsAny<PersonChangeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonCreateResult(PersonCreateStatus.Accepted, created));
        var handler = _mocker.CreateInstance<PutPersonHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("PUT");

        // Act
        var result = await handler.Handle(
            new HandlerContext(req.Object, null),
            new PersonChangeRequest { Name = name },
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await ReadBodyAsync(result)).Should().BeEmpty();
    }
}
