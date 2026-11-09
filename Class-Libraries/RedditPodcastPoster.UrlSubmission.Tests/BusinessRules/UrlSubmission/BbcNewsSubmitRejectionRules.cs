using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class BbcNewsSubmitRejectionRules
{
    private readonly DomainTestFixture _fixture = new();

    [Theory(DisplayName =
        "A BBC News page is rejected for submit on bbc.co.uk and bbc.com (apex, www., m., feeds.), whatever the casing, " +
        "for articles, /news/av video pages, query strings and fragments, the bare /news section, and the legacy news.bbc.co.uk host, " +
        "because BBC news comes in only via BBC iPlayer or the BBC News YouTube channel (#1211 G5).")]
    [InlineData("https://bbc.co.uk/news/{0}")]
    [InlineData("https://bbc.com/news/{0}")]
    [InlineData("https://www.bbc.co.uk/news/{0}")]
    [InlineData("https://www.bbc.com/news/{0}")]
    [InlineData("https://m.bbc.co.uk/news/{0}")]
    [InlineData("https://m.bbc.com/news/{0}")]
    [InlineData("https://feeds.bbc.co.uk/news/{0}")]
    [InlineData("https://WWW.BBC.CO.UK/NEWS/{0}")]
    [InlineData("https://www.bbc.co.uk/news/av/{0}")]
    [InlineData("https://www.bbc.com/news/articles/{0}")]
    [InlineData("https://www.bbc.co.uk/news/uk-{0}?at_medium=RSS&at_campaign=rss")]
    [InlineData("https://www.bbc.co.uk/news/uk-{0}#comments")]
    [InlineData("https://www.bbc.co.uk/news")]
    [InlineData("https://www.bbc.com/news/")]
    [InlineData("https://news.bbc.co.uk/2/hi/{0}.stm")]
    [InlineData("https://news.bbc.co.uk/")]
    public void bbc_news_pages_are_rejected(string template)
    {
        // Arrange
        var url = new Uri(string.Format(template, _fixture.CreateGuid().ToString("N")));

        // Act
        var result = BbcNewsSubmitRejection.Applies(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory(DisplayName =
        "Other BBC pages and look-alike hosts are not rejected as BBC News, " +
        "because Newsround, Newsbeat, Sport, iPlayer and Sounds are not /news/ pages and a look-alike host is not the BBC.")]
    [InlineData("https://www.bbc.co.uk/newsround/{0}")]
    [InlineData("https://www.bbc.co.uk/newsbeat/{0}")]
    [InlineData("https://www.bbc.com/sport/{0}")]
    [InlineData("https://www.bbc.co.uk/iplayer/episode/{0}")]
    [InlineData("https://www.bbc.co.uk/sounds/play/{0}")]
    [InlineData("https://www.notbbc.co.uk/news/{0}")]
    [InlineData("https://bbc.co.uk.example.com/news/{0}")]
    [InlineData("https://www.example.com/news/{0}")]
    public void other_pages_are_not_rejected(string template)
    {
        // Arrange
        var url = new Uri(string.Format(template, _fixture.CreateGuid().ToString("N")));

        // Act
        var result = BbcNewsSubmitRejection.Applies(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "The rejected submit outcome creates nothing, is a NewsReport, and carries the message pointing to BBC iPlayer " +
        "or the BBC News YouTube channel.")]
    public void rejected_result_carries_the_message()
    {
        // Arrange
        var expectedMessage = BbcNewsSubmitRejection.Message;

        // Act
        var result = BbcNewsSubmitRejection.Result();

        // Assert
        result.Rejected.Should().BeTrue();
        result.RejectionMessage.Should().Be(expectedMessage);
        result.RejectionMessage.Should().Contain("BBC iPlayer").And.Contain("BBC News YouTube channel");
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.Episode.Should().BeNull();
        result.Podcast.Should().BeNull();
        result.PlayableId.Should().BeNull();
    }
}
