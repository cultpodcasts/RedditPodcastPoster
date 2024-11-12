using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects.HashTags;

namespace RedditPodcastPoster.Subjects.Tests.HashTags;

public class HashTagProviderTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private HashTagProvider Sut => _mocker.CreateInstance<HashTagProvider>();

    [Theory]
    [InlineData("Term A", "#TermA")]
    [InlineData("Term's A", "#TermsA")]
    [InlineData("Term-A", "#TermA")]
    public async Task GetHashTags_WithEnrichmentHashTag_IsCorrect(string enrichmentHashTag, string expected)
    {
        // arrange
        var subject = new Subject(_fixture.Create<string>())
        {
            EnrichmentHashTags = [enrichmentHashTag]
        };
        _mocker.GetMock<ISubjectRepository>().Setup(x => x.GetByName(It.IsAny<string>()))
            .ReturnsAsync(subject);
        // act
        var result = await Sut.GetHashTags([_fixture.Create<string>()]);
        // assert
        result.Should().BeEquivalentTo([new HashTag(enrichmentHashTag, expected)]);
    }
}