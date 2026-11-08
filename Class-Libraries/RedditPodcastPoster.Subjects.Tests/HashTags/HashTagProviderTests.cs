using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.HashTags;

namespace RedditPodcastPoster.Subjects.Tests.HashTags;

public class HashTagProviderTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private HashTagProvider Sut => _mocker.CreateInstance<HashTagProvider>();

    [Theory(DisplayName =
        "GetHashTags: a permitted enrichment phrase becomes a HashTag with MatchingText compacted by removing whitespace only, because that body is the X and Bluesky hashtag.")]
    [InlineData("Term A", "#TermA")]
    [InlineData("Alpha Beta", "#AlphaBeta")]
    public async Task GetHashTags_WithPermittedEnrichmentHashTag_IsCorrect(string enrichmentHashTag, string expected)
    {
        // Arrange
        var subject = new Subject(_fixture.Create<string>())
        {
            EnrichmentHashTags = [enrichmentHashTag]
        };
        _mocker.GetMock<ISubjectRepository>().Setup(x => x.GetByName(It.IsAny<string>()))
            .ReturnsAsync(subject);

        // Act
        var result = await Sut.GetHashTags([_fixture.Create<string>()]);

        // Assert
        result.Should().BeEquivalentTo([new HashTag(enrichmentHashTag, expected)]);
    }

    [Theory(DisplayName =
        "GetHashTags: enrichment phrases that compact to a body with disallowed characters are not returned as HashTags, because those tags are not permitted on X and Bluesky.")]
    [InlineData("Term-A")]
    [InlineData("Term's A")]
    [InlineData("Q&A")]
    public async Task GetHashTags_WithNonPermittedEnrichmentHashTag_IsEmpty(string enrichmentHashTag)
    {
        // Arrange
        var subject = new Subject(_fixture.Create<string>())
        {
            EnrichmentHashTags = [enrichmentHashTag]
        };
        _mocker.GetMock<ISubjectRepository>().Setup(x => x.GetByName(It.IsAny<string>()))
            .ReturnsAsync(subject);

        // Act
        var result = await Sut.GetHashTags([_fixture.Create<string>()]);

        // Assert
        result.Should().BeEmpty();
    }
}
