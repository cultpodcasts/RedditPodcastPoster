using AutoFixture;
using Moq.AutoMock;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests
{
    public class YouTubeSearcherTests
    {
        private readonly Fixture _fixture;
        private readonly AutoMocker _mocker;

        public YouTubeSearcherTests()
        {
            _fixture = new Fixture();
            _mocker = new AutoMocker();
        }

        private IYouTubeSearcher Sut => _mocker.CreateInstance<YouTubeSearcher>();


    }
}