using AutoFixture;
using FluentAssertions;
using Moq.AutoMock;

namespace RedditPodcastPoster.Text.Tests;

public class HashTagEnricherTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private HashTagEnricher Sut => _mocker.CreateInstance<HashTagEnricher>();

    [Fact]
    public void AddHashTag_WithNoMatch_StringIsCorrect()
    {
        // arrange
        var input = _fixture.Create<string>();
        // act
        var (result, _) = Sut.AddHashTag(input, _fixture.Create<string>());
        // assert
        result.Should().Be(input);
    }

    [Fact]
    public void AddHashTag_WithNoMatch_StatusIsCorrect()
    {
        // arrange
        var input = _fixture.Create<string>();
        // act
        var (_, changed) = Sut.AddHashTag(input, _fixture.Create<string>());
        // assert
        changed.Should().BeFalse();
    }

    [Fact]
    public void AddHashTag_WithMatchOfSameCase_StringIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix Term Suffix";
        // act
        var (result, _) = Sut.AddHashTag(input, hashTag);
        // assert
        result.Should().Be("Prefix #Term Suffix");
    }

    [Fact]
    public void AddHashTag_WithMatchWithApostropheOfSameCase_StringIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix Term's Suffix";
        // act
        var (result, _) = Sut.AddHashTag(input, hashTag);
        // assert
        result.Should().Be("Prefix #Term's Suffix");
    }

    [Fact]
    public void AddHashTag_WithMatchOfSameCase_StatusIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix Term Suffix";
        // act
        var (_, changed) = Sut.AddHashTag(input, hashTag);
        // assert
        changed.Should().BeTrue();
    }

    [Fact]
    public void AddHashTag_WithMatchesOfSameCase_StringIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix Term Middle Term Suffix";
        // act
        var (result, _) = Sut.AddHashTag(input, hashTag);
        // assert
        result.Should().Be("Prefix #Term Middle Term Suffix");
    }

    [Fact]
    public void AddHashTag_WithMatchesOfSameCase_StatusIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix Term Middle Term Suffix";
        // act
        var (_, changed) = Sut.AddHashTag(input, hashTag);
        // assert
        changed.Should().BeTrue();
    }

    [Fact]
    public void AddHashTag_WithMatchOfDifferentCase_StringIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix TERM Suffix";
        // act
        var (result, _) = Sut.AddHashTag(input, hashTag);
        // assert
        result.Should().Be("Prefix #Term Suffix");
    }

    [Fact]
    public void AddHashTag_WithMatchOfDifferentCase_StatusIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix TERM Suffix";
        // act
        var (_, changed) = Sut.AddHashTag(input, hashTag);
        // assert
        changed.Should().BeTrue();
    }

    [Fact]
    public void AddHashTag_WithMatchesOfDifferentCase_StringIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix TERM Middle TeRm Suffix";
        // act
        var (result, _) = Sut.AddHashTag(input, hashTag);
        // assert
        result.Should().Be("Prefix #Term Middle TeRm Suffix");
    }

    [Fact]
    public void AddHashTag_WithMatchesOfDifferentCase_StatusIsCorrect()
    {
        // arrange
        var hashTag = "Term";
        var input = "Prefix TERM Middle TeRm Suffix";
        // act
        var (_, changed) = Sut.AddHashTag(input, hashTag);
        // assert
        changed.Should().BeTrue();
    }
}