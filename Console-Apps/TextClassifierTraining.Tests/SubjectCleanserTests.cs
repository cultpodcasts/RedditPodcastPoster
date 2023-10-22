using AutoFixture;
using FluentAssertions;
using Moq.AutoMock;

namespace TextClassifierTraining.Tests;

public class SubjectCleanserTests
{
    private readonly Fixture _fixture;
    private readonly AutoMocker _mocker;

    public SubjectCleanserTests()
    {
        _fixture = new Fixture();
        _mocker = new AutoMocker();
    }

    private ISubjectCleanser Sut => _mocker.CreateInstance<SubjectCleanser>();

    [Fact]
    public async Task CleanseSubjects_WithRegularSubject_IsCorrect()
    {
        // arrange
        var subject = "expected";
        // act
        var result = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        result.SingleOrDefault().Should().Be(subject);
    }

    [Fact]
    public async Task CleanseSubjects_WithDuplicateRegularSubjects_IsCorrect()
    {
        // arrange
        var subject = "expected";
        // act
        var result = await Sut.CleanSubjects(new List<string> {subject, subject});
        // assert
        result.SingleOrDefault().Should().Be(subject);
    }

    [Fact]
    public async Task CleanseSubjects_WithComplexSubject_IsCorrect()
    {
        // arrange
        var subject = "term 1 (term2)  / term 3";
        // act
        var results = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        results.Should().BeEquivalentTo("term 1", "term2", "term 3");
    }

    [Fact]
    public async Task CleanseSubjects_WithForwardSlash_IsCorrect()
    {
        // arrange
        var subject = "term 1   / term 2";
        // act
        var results = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        results.Should().BeEquivalentTo("term 1", "term 2");
    }

    [Fact]
    public async Task CleanseSubjects_WithBackSlash_IsCorrect()
    {
        // arrange
        var subject = "term1   \\ term2";
        // act
        var results = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        results.Should().BeEquivalentTo("term1", "term2");
    }

    [Theory]
    [InlineData("Term 1 \u0026 Term 2", "term 1 and term 2")]
    [InlineData("Term 1 & Term 2", "term 1 and term 2")]
    public async Task CleanseSubjects_WithAmpsersand_IsCorrect(string subject, string expected)
    {
        // arrange
        // act
        var results = await Sut.CleanSubjects(new List<string> {subject});
        // assert
        results.Should().BeEquivalentTo("term 1", "term 2");
    }
}