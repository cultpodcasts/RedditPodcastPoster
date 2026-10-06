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
    private const string StoredName = "Alpha Beta";

    private readonly AutoMocker _mocker = new();
    private SubjectEntity? _lookupResult;

    public SubjectGetRouteNameRules()
    {
        _mocker.Use(NullLogger<SubjectGetService>.Instance);
        _mocker.GetMock<ISubjectRepository>()
            .Setup(r => r.GetByName(It.IsAny<string>()))
            .ReturnsAsync(() => _lookupResult);
    }

    [Fact(DisplayName =
        "A subject looked up from a route name with a percent-encoded space resolves to the stored name, because the route segment is encoded and the catalogue name contains a space.")]
    public async Task percent_encoded_space_matches_the_stored_name()
    {
        // Arrange
        var stored = new SubjectEntity(StoredName);
        _lookupResult = stored;
        var sut = _mocker.CreateInstance<SubjectGetService>();

        // Act
        var result = await sut.GetAsync("Alpha%20Beta", CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectGetStatus.Ok);
        result.Subject.Should().BeSameAs(stored);
        _mocker.GetMock<ISubjectRepository>().Verify(r => r.GetByName(StoredName), Times.Once);
    }

    [Fact(DisplayName =
        "A subject route name that was percent-encoded twice still resolves to the stored name, because a gateway may encode the segment again.")]
    public async Task double_encoded_space_matches_the_stored_name()
    {
        // Arrange
        var stored = new SubjectEntity(StoredName);
        _lookupResult = stored;
        var sut = _mocker.CreateInstance<SubjectGetService>();

        // Act
        var result = await sut.GetAsync("Alpha%2520Beta", CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectGetStatus.Ok);
        result.Subject.Should().BeSameAs(stored);
        _mocker.GetMock<ISubjectRepository>().Verify(r => r.GetByName(StoredName), Times.Once);
    }

    [Fact(DisplayName =
        "A subject route that decodes to a different name is not found, because lookup uses that decoded name and it is not the stored subject.")]
    public async Task decoded_name_that_is_not_stored_is_not_found()
    {
        // Arrange
        _lookupResult = null;
        var sut = _mocker.CreateInstance<SubjectGetService>();

        // Act
        var result = await sut.GetAsync("Gamma%20Delta", CancellationToken.None);

        // Assert
        result.Status.Should().Be(SubjectGetStatus.NotFound);
        result.Subject.Should().BeNull();
        _mocker.GetMock<ISubjectRepository>().Verify(r => r.GetByName("Gamma Delta"), Times.Once);
    }
}
