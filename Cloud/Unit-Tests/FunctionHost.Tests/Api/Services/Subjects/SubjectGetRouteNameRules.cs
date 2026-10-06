using System.Linq.Expressions;
using Api.Models;
using Api.Services.Subjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;
using SubjectEntity = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Services.Subjects;

public class SubjectGetRouteNameRules
{
    private readonly AutoMocker _mocker = new();

    public SubjectGetRouteNameRules()
    {
        _mocker.Use(NullLogger<SubjectGetService>.Instance);
    }

    [Fact(DisplayName =
        "A subject looked up from a route name with a percent-encoded space resolves to the stored name, because the route segment is encoded and the catalogue name contains a space.")]
    public async Task percent_encoded_space_matches_the_stored_name()
    {
        // Arrange
        var stored = new SubjectEntity("Alpha Beta");
        _mocker.GetMock<ISubjectRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<SubjectEntity, bool>>>()))
            .ReturnsAsync((Expression<Func<SubjectEntity, bool>> selector) =>
                selector.Compile().Invoke(stored) ? stored : null);
        var sut = _mocker.CreateInstance<SubjectGetService>();

        // Act
        var result = await sut.GetAsync("Alpha%20Beta", CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectGetStatus.Ok);
        result.Subject.Should().BeSameAs(stored);
    }

    [Fact(DisplayName =
        "A subject route name that was percent-encoded twice still resolves to the stored name, because a gateway may encode the segment again.")]
    public async Task double_encoded_space_matches_the_stored_name()
    {
        // Arrange
        var stored = new SubjectEntity("Alpha Beta");
        _mocker.GetMock<ISubjectRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<SubjectEntity, bool>>>()))
            .ReturnsAsync((Expression<Func<SubjectEntity, bool>> selector) =>
                selector.Compile().Invoke(stored) ? stored : null);
        var sut = _mocker.CreateInstance<SubjectGetService>();

        // Act
        var result = await sut.GetAsync("Alpha%2520Beta", CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectGetStatus.Ok);
        result.Subject.Should().BeSameAs(stored);
    }
}
