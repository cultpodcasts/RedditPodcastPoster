using System.Linq.Expressions;
using System.Net;
using Azure.Core.Serialization;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Dtos;
using Api.Handlers;
using Reddit.Exceptions;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Reddit.Clients;
using RedditPodcastPoster.Reddit.Configuration;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Services;
using Xunit;
using SubjectEntity = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Handlers;

public class SubjectHandlerTests
{
    private readonly AutoMocker _mocker = new();

    public SubjectHandlerTests()
    {
        _mocker.Use(Options.Create(new SubredditSettings { SubredditName = "testsubreddit" }));
    }

    private SubjectHandler Sut => _mocker.CreateInstance<SubjectHandler>();

    [Fact]
    public async Task Put_WhenRedditFlairUpdateForbidden_StillCreatesSubject()
    {
        // arrange
        var flairId = Guid.NewGuid();
        var entity = new SubjectEntity("Topic") { Id = Guid.NewGuid() };
        _mocker.GetMock<ISubjectFactory>()
            .Setup(x => x.Create("Topic", null, null, null))
            .ReturnsAsync(entity);
        _mocker.GetMock<ISubjectService>()
            .Setup(x => x.Match(It.IsAny<SubjectEntity>()))
            .ReturnsAsync((SubjectEntity?)null);
        _mocker.GetMock<IAdminRedditClient>()
            .Setup(x => x.Client)
            .Throws(new RedditForbiddenException("Reddit API returned Forbidden (403) response."));
        var (req, _) = CreateRequestResponse("PUT");

        // act
        var result = await Sut.Put(
            req.Object,
            new Subject { Name = "Topic", RedditFlairTemplateId = flairId },
            null,
            CancellationToken.None);

        // assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _mocker.GetMock<ISubjectRepository>().Verify(
            x => x.Save(It.Is<SubjectEntity>(s => s.RedditFlairTemplateId == flairId)),
            Times.Once);
        _mocker.GetMock<ISubjectsPublisher>().Verify(x => x.PublishSubjects(), Times.Once);
    }

    [Fact]
    public async Task Post_WhenRedditFlairUpdateForbidden_StillUpdatesSubject()
    {
        // arrange
        var subjectId = Guid.NewGuid();
        var flairId = Guid.NewGuid();
        var existing = new SubjectEntity("Topic") { Id = subjectId };
        _mocker.GetMock<ISubjectRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<SubjectEntity, bool>>>()))
            .ReturnsAsync(existing);
        _mocker.GetMock<IAdminRedditClient>()
            .Setup(x => x.Client)
            .Throws(new RedditForbiddenException("Reddit API returned Forbidden (403) response."));
        var (req, _) = CreateRequestResponse("POST");

        // act
        var result = await Sut.Post(
            req.Object,
            new SubjectChangeRequestWrapper(subjectId, new Subject { RedditFlairTemplateId = flairId }),
            null,
            CancellationToken.None);

        // assert
        result.StatusCode.Should().Be(HttpStatusCode.Accepted);
        existing.RedditFlairTemplateId.Should().Be(flairId);
        _mocker.GetMock<ISubjectRepository>().Verify(x => x.Save(existing), Times.Once);
    }

    private static (Mock<HttpRequestData> Req, Mock<HttpResponseData> Response) CreateRequestResponse(
        string method)
    {
        var services = new ServiceCollection();
        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer();
        });
        var serviceProvider = services.BuildServiceProvider();

        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.InstanceServices).Returns(serviceProvider);
        var functionDefinition = new Mock<FunctionDefinition>();
        functionDefinition.SetupGet(d => d.Name).Returns("TestFunction");
        context.SetupGet(c => c.FunctionDefinition).Returns(functionDefinition.Object);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var response = new Mock<HttpResponseData>(context.Object);
        response.SetupProperty(r => r.StatusCode, HttpStatusCode.OK);
        response.SetupProperty(r => r.Headers, new HttpHeadersCollection());
        response.Setup(r => r.Body).Returns(new MemoryStream());

        var req = new Mock<HttpRequestData>(context.Object);
        req.Setup(r => r.CreateResponse()).Returns(response.Object);
        req.Setup(r => r.Url).Returns(new Uri("https://localhost/api/subject"));
        req.Setup(r => r.Method).Returns(method);
        req.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
        req.Setup(r => r.Body).Returns(new MemoryStream());

        return (req, response);
    }
}
