using AutoFixture;
using Moq.AutoMock;

namespace RedditPodcastPoster.BBC.Tests;

public class SoundsPageMetaDataExtractorTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private SoundsPageMetaDataExtractor Sut => _mocker.CreateInstance<SoundsPageMetaDataExtractor>();


    [Fact]
    public async Task Extract_WithLiveUrl_IsCorrect()
    {
        //// arrange
        //var url = new Uri("https://www.bbc.co.uk/sounds/play/p0m9y36q");
        //var httpClient = new HttpClient();
        //var response = await httpClient.GetAsync(url);
        //// act
        //var metaData = await Sut.Extract(url, response);
        //// assert
    }
}