using AutoFixture;
using Moq.AutoMock;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using Xunit;
using FluentAssertions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests
{
    public class EpisodeHelperTests
    {
        private readonly Fixture _fixture;
        private readonly AutoMocker _mocker;

        public EpisodeHelperTests()
        {
            _fixture = new Fixture();
            _mocker = new AutoMocker();
        }

        private IEpisodeHelper Sut => _mocker.CreateInstance<EpisodeHelper>();

        [Fact]
        public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndAlreadySpotifyAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, "prefix " + substring + " suffix")
                .With(x => x.SpotifyId, "spotifyid")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    Spotify = new Uri("http://existing-url")
                })
                .Create();
            var spotifyItem = _fixture.Build<ResolvedSpotifyItem>()
                .With(x => x.EpisodeTitle, substring)
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.Spotify)
                .With(x => x.ResolvedSpotifyItem, spotifyItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedYouTubeItem, (ResolvedYouTubeItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndNotAlreadySpotifyAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, "prefix " + substring + " suffix")
                .With(x => x.SpotifyId, "")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    Spotify = null
                })
                .Create();
            var spotifyItem = _fixture.Build<ResolvedSpotifyItem>()
                .With(x => x.EpisodeTitle, substring)
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.Spotify)
                .With(x => x.ResolvedSpotifyItem, spotifyItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedYouTubeItem, (ResolvedYouTubeItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeTrue();
        }


        [Fact]
        public void IsMatchingEpisode_WhenContainsEpisodeNameAndAlreadySpotifyAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, substring)
                .With(x => x.SpotifyId, "spotifyid")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    Spotify = new Uri("http://existing-url")
                })
                .Create();
            var spotifyItem = _fixture.Build<ResolvedSpotifyItem>()
                .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.Spotify)
                .With(x => x.ResolvedSpotifyItem, spotifyItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedYouTubeItem, (ResolvedYouTubeItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatchingEpisode_WhenContainsEpisodeNameAndNotAlreadySpotifyAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, substring)
                .With(x => x.SpotifyId, "")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    Spotify = null
                })
                .Create();
            var spotifyItem = _fixture.Build<ResolvedSpotifyItem>()
                .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.Spotify)
                .With(x => x.ResolvedSpotifyItem, spotifyItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedYouTubeItem, (ResolvedYouTubeItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndAlreadyYouTubeAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, "prefix " + substring + " suffix")
                .With(x => x.YouTubeId, "youtubeid")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    YouTube = new Uri("http://existing-url")
                })
                .Create();
            var youTubeItem = _fixture.Build<ResolvedYouTubeItem>()
                .With(x => x.EpisodeTitle, substring)
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.YouTube)
                .With(x => x.ResolvedYouTubeItem, youTubeItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedSpotifyItem, (ResolvedSpotifyItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndNotAlreadyYouTubeAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, "prefix " + substring + " suffix")
                .With(x => x.YouTubeId, "")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    YouTube = null
                })
                .Create();
            var youTubeItem = _fixture.Build<ResolvedYouTubeItem>()
                .With(x => x.EpisodeTitle, substring)
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.YouTube)
                .With(x => x.ResolvedYouTubeItem, youTubeItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedSpotifyItem, (ResolvedSpotifyItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeTrue();
        }


        [Fact]
        public void IsMatchingEpisode_WhenContainsEpisodeNameAndAlreadyYouTubeAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, substring)
                .With(x => x.YouTubeId, "youtubeid")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    YouTube = new Uri("http://existing-url")
                })
                .Create();
            var youTubeItem = _fixture.Build<ResolvedYouTubeItem>()
                .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.YouTube)
                .With(x => x.ResolvedYouTubeItem, youTubeItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedSpotifyItem, (ResolvedSpotifyItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsMatchingEpisode_WhenContainsEpisodeNameAndNotAlreadyYouTubeAssigned_IsCorrect()
        {
            // arrange
            var substring = "component";
            var episode = _fixture.Build<Models.Episode>()
                .With(x => x.Title, substring)
                .With(x => x.YouTubeId, "")
                .With(x => x.Urls, new Models.ServiceUrls()
                {
                    YouTube = null
                })
                .Create();
            var youTubeItem = _fixture.Build<ResolvedYouTubeItem>()
                .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
                .Create();
            var categorisedItem = _fixture.Build<CategorisedItem>()
                .With(x => x.Authority, Models.Service.YouTube)
                .With(x => x.ResolvedYouTubeItem, youTubeItem)
                .With(x => x.ResolvedAppleItem, (ResolvedAppleItem?)null)
                .With(x => x.ResolvedSpotifyItem, (ResolvedSpotifyItem?)null)
                .Create();
            // act
            var result = Sut.IsMatchingEpisode(episode, categorisedItem);
            // assert
            result.Should().BeTrue();
        }
    }
}
