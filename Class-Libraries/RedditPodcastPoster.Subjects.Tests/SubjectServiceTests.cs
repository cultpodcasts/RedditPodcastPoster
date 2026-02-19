using AutoFixture;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects.Tests;

public class SubjectServiceTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;
    private IList<Subject> _subjects;

    public SubjectServiceTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();
        _subjects = _fixture.CreateMany<Subject>().ToList();
        _mocker.GetMock<ISubjectsProvider>().Setup(x => x.GetAll())
            .Returns(() => _subjects.ToAsyncEnumerable());
    }

    private ISubjectService Sut => _mocker.CreateInstance<SubjectService>();

    [Fact]
    public async Task Match_WithUnmatchedSubject_IsCorrect()
    {
        // arrange
        var subject = _fixture.Create<Subject>();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Match_WithMatchingIds_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Id, _subjects.Last().Id).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithMatchingNames_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Name, _subjects.Last().Name).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithSubjectNameMatchingASubjectAlias_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Name, _subjects.Last().Aliases!.Last).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithSubjectAliasMatchingASubjectName_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Aliases, new[] { _subjects.Last().Name }).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithSubjectAliasMatchingASubjectAlias_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Aliases, new[] { _subjects.Last().Aliases!.Last() })
            .Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithSubjectAliasMatchingAMultipleSubjectsAlias_IsCorrect()
    {
        // arrange
        var subject = _fixture.Create<Subject>();
        var firstMatchingAlias =
            _fixture.Build<Subject>().With(x => x.Aliases, new[] { subject.Aliases!.First() }).Create();
        var secondMatchingAlias =
            _fixture.Build<Subject>().With(x => x.Aliases, new[] { subject.Aliases!.Last() }).Create();
        _subjects = _subjects.Append(firstMatchingAlias).Append(secondMatchingAlias).ToList();
        // act
        Func<Task> act = () => Sut.Match(subject);
        // assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Match_WithSubjectNameMatchingASubjectAssociatedSubject_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Name, _subjects.Last().AssociatedSubjects!.Last).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }
}