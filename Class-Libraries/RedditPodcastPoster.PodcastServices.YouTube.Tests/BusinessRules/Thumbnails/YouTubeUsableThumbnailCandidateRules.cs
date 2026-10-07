using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Thumbnails;

public class YouTubeUsableThumbnailCandidateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Usable YouTube thumbnail candidates are largest-first and omit a non-default image at the placeholder height, because the card tries a smaller image after the tall ones.")]
    public void usable_candidates_are_largest_first_and_drop_the_placeholder_tier()
    {
        // Arrange
        var tallest = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var next = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var placeholder = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var fallback = new Uri($"https://cdn.example.test/{_fixture.CreateYouTubeId()}");
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Snippet = new VideoSnippet
            {
                Thumbnails = new ThumbnailDetails
                {
                    Maxres = new Thumbnail
                    {
                        Url = tallest.AbsoluteUri,
                        Height = YouTubeThumbnailValidation.PlaceholderMaxHeight + 200
                    },
                    High = new Thumbnail
                    {
                        Url = next.AbsoluteUri,
                        Height = YouTubeThumbnailValidation.PlaceholderMaxHeight + 1
                    },
                    Medium = new Thumbnail
                    {
                        Url = placeholder.AbsoluteUri,
                        Height = YouTubeThumbnailValidation.PlaceholderMaxHeight
                    },
                    Default__ = new Thumbnail
                    {
                        Url = fallback.AbsoluteUri,
                        Height = YouTubeThumbnailValidation.PlaceholderMaxHeight
                    }
                }
            }
        };
        var sut = _mocker.CreateInstance<YouTubeThumbnailResolver>();

        // Act
        var urls = sut.GetUsableCandidateUrls(video);

        // Assert
        urls.Should().Equal(tallest, next, fallback);
    }
}
