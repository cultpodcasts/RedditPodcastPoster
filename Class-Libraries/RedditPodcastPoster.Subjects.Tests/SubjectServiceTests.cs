using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects.Tests;

public class SubjectServiceTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;
    private readonly IEnumerable<Subject> _subjects;

    public SubjectServiceTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();
        _subjects = _fixture.CreateMany<Subject>();
        _mocker.GetMock<ICachedSubjectRepository>().Setup(x => x.GetAll(It.IsAny<string>()))
            .ReturnsAsync(() => _subjects);
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
        var subject = _fixture.Build<Subject>().With(x => x.Name, _subjects.Last().Aliases.Last).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithSubjectAliasMatchingASubjectName_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Aliases, new[] {_subjects.Last().Name}).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }

    [Fact]
    public async Task Match_WithSubjectAliasMatchingASubjectAlias_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Aliases, new[] {_subjects.Last().Aliases.Last()}).Create();
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
        _subjects.Append(_fixture.Build<Subject>().With(x => x.Aliases, new[] {subject.Aliases.First()}).Create());
        _subjects.Append(_fixture.Build<Subject>().With(x => x.Aliases, new[] {subject.Aliases.Last()}).Create());
        // act
        Func<Task> act = () => Sut.Match(subject);
        // assert
        act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Match_WithSubjectNameMatchingASubjectAssociatedSubject_IsCorrect()
    {
        // arrange
        var subject = _fixture.Build<Subject>().With(x => x.Name, _subjects.Last().AssociatedSubjects.Last).Create();
        // act
        var result = await Sut.Match(subject);
        // assert
        result.Should().Be(_subjects.Last());
    }
}