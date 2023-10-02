using FluentAssertions;
using RedditPodcastPoster.Common.Text;
using Xunit;

namespace RedditPodcastPoster.Common.Tests.Text;

public class KnownTermsTests
{
    [Fact]
    public void MaintainKnownTerms_WithKnownTerm_IsCorrect()
    {
        // arrange
        // act
        var result = KnownTerms.MaintainKnownTerms("lorem Pbcc ipsum");
        // assert
        result.Should().Contain("PBCC");
    }
}