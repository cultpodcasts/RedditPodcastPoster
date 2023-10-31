using FluentAssertions;

namespace RedditPodcastPoster.Text.Tests
{
    public class LowerCaseTermsTests
    {
        [Fact]
        public void Expressions_WhenEvaluated_IsCorrect()
        {
            // arrange
            // act
            Action act = () =>
            {
                var x = LowerCaseTerms.Expressions;
            };
            // assert
            act.Should().NotThrow();
        }
    }
}
