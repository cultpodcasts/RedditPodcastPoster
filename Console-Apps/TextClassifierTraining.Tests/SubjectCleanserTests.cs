using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;

namespace TextClassifierTraining.Tests;

public class SubjectCleanserTests
{
    private readonly AutoMocker _mocker = new();

    private ISubjectCleanser Sut => _mocker.CreateInstance<SubjectCleanser>();

    [Fact]
    public async Task CleanseSubjects_WithRegularSubject_IsCorrect()
    {
        // arrange
        var subject = "expected";
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match(subject))
            .ReturnsAsync(() => SubjectFactory.Create(subject));

        // act
        var (unmatched, result) = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        result.SingleOrDefault().Should().Be(subject);
    }

    [Fact]
    public async Task CleanseSubjects_WithDuplicateRegularSubjects_IsCorrect()
    {
        // arrange
        var subject = "expected";
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match(subject))
            .ReturnsAsync(() => SubjectFactory.Create(subject));

        // act
        var (unmatched, result) = await Sut.CleanSubjects(new List<string> {subject, subject});
        // assert
        result.SingleOrDefault().Should().Be(subject);
    }

    [Fact]
    public async Task CleanseSubjects_WithComplexSubject_IsCorrect()
    {
        // arrange
        var subject = "term 1 (term 2)  / term 3";
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 1"))
            .ReturnsAsync(() => SubjectFactory.Create("term 1"));
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 2"))
            .ReturnsAsync(() => SubjectFactory.Create("term 2"));
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 3"))
            .ReturnsAsync(() => SubjectFactory.Create("term 3"));

        // act
        var (unmatched, results) = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        results.Should().BeEquivalentTo("term 1", "term 2", "term 3");
    }

    [Fact]
    public async Task CleanseSubjects_WithForwardSlash_IsCorrect()
    {
        // arrange
        var subject = "term 1   / term 2";
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 1"))
            .ReturnsAsync(() => SubjectFactory.Create("term 1"));
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 2"))
            .ReturnsAsync(() => SubjectFactory.Create("term 2"));
        // act
        var (unmatched, result) = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        result.Should().BeEquivalentTo("term 1", "term 2");
    }

    [Fact]
    public async Task CleanseSubjects_WithBackSlash_IsCorrect()
    {
        // arrange
        var subject = "term 1   \\ term 2";
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 1"))
            .ReturnsAsync(() => SubjectFactory.Create("term 1"));
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 2"))
            .ReturnsAsync(() => SubjectFactory.Create("term 2"));
        // act
        var (unmatched, result) = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        result.Should().BeEquivalentTo("term 1", "term 2");
    }

    [Theory]
    [InlineData("Term 1 & Term 2", new[] {"term 1", "term 2"})]
    public async Task CleanseSubjects_WithAmpsersand_IsCorrect(string subject, string[] expected)
    {
        // arrange
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 1"))
            .ReturnsAsync(() => SubjectFactory.Create("term 1"));
        _mocker
            .GetMock<ISubjectService>()
            .Setup(x => x.Match("term 2"))
            .ReturnsAsync(() => SubjectFactory.Create("term 2"));
        // act
        var (unmatched, result) = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        result.Should().BeEquivalentTo(expected);
    }
}