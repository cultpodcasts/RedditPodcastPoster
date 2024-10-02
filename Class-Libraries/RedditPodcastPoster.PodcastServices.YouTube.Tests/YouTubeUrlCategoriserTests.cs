using AutoFixture;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeUrlCategoriserTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IYouTubeUrlCategoriser Sut => _mocker.CreateInstance<YouTubeUrlCategoriser>();

    [Fact(Skip = "Scaffold for test")]
    public async Task Resolve()
    {
        // arrange
        var episodes = new List<Episode>
        {
            new()
            {
                Title = "1880: Do Mormon Women have More Power & Authority than Those of Other Faiths?"
            }
        };
        var criteria = _fixture.Build<PodcastServiceSearchCriteria>().With(x => x.EpisodeTitle, episodes.First().Title)
            .Create();
        var matchingPodcast = _fixture.Build<Podcast>().With(x => x.Episodes, episodes).Create();
        var indexingContext = _fixture.Create<IndexingContext>();
        _mocker.GetMock<IYouTubeChannelVideosService>().Setup(x =>
            x.GetChannelVideos(It.IsAny<YouTubeChannelId>(), It.IsAny<IndexingContext>())).ReturnsAsync(
            new ChannelVideos(new Channel(), new List<PlaylistItem>
            {
                new()
                {
                    Snippet = new PlaylistItemSnippet
                    {
                        Title =
                            "Do Mormon Women have More Power & Authority than Other Women? \u202a@breakingdownpatriarchy\u202c | Ep. 1880",
                        PublishedAtDateTimeOffset = episodes.First().Release
                    },
                    Id = "new-episode-id"
                }
            })
        );
        // act
        var result = await Sut.Resolve(
            criteria,
            matchingPodcast,
            indexingContext);
        // assert
    }
}