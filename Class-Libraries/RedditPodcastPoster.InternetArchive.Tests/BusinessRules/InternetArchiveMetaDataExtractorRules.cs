using System.Net;
using System.Text;
using FluentAssertions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.InternetArchive.Models;
using RedditPodcastPoster.InternetArchive.Providers;

namespace RedditPodcastPoster.InternetArchive.Tests.BusinessRules;

public class InternetArchiveMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private IEnumerable<PlayListItem> _playlist = [];

    public InternetArchiveMetaDataExtractorRules()
    {
        _mocker.Use<IInternetArchivePlayListProvider>(new StubPlayListProvider(() => _playlist));
        _mocker.Use(NullLogger<MetaDataExtractor>.Instance);
    }

    [Fact(DisplayName =
        "Internet Archive extract leaves ShowName null for a single-item page, " +
        "because the item title is not a distinct collection/series name and the uploader is publisher only.")]
    public async Task single_item_page_has_no_show_name()
    {
        // Arrange
        var itemTitle = _fixture.CreateTitle();
        var uploader = _fixture.Create<string>();
        _playlist =
        [
            new PlayListItem
            {
                Title = itemTitle,
                Orig = $"/download/{_fixture.CreateYouTubeId()}",
                Duration = TimeSpan.FromMinutes(42)
            }
        ];
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
        var html =
            $"<html><body>" +
            $"<span itemprop=\"name\">{itemTitle}</span>" +
            $"<section class=\"item-upload-info\"><p><a class=\"item-upload-info__uploader-name\">{uploader}</a></p></section>" +
            $"</body></html>";
        var sut = _mocker.CreateInstance<MetaDataExtractor>();

        // Act
        var meta = await sut.Extract(url, HtmlResponse(html));

        // Assert
        meta.Title.Should().Be(itemTitle);
        meta.Publisher.Should().Be(uploader);
        meta.ShowName.Should().BeNull();
    }

    [Fact(DisplayName =
        "Internet Archive extract sets ShowName to the collection page title when a playlist has multiple items " +
        "and the matched episode title differs, so lookup can return podcastName for archive playlists.")]
    public async Task multi_item_playlist_uses_collection_title_as_show_name()
    {
        // Arrange
        var collectionTitle = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var orig = $"/download/{_fixture.CreateYouTubeId()}.mp3";
        _playlist =
        [
            new PlayListItem
            {
                Title = _fixture.CreateTitle(),
                Orig = $"/download/{_fixture.CreateYouTubeId()}.mp3",
                Duration = TimeSpan.FromMinutes(10)
            },
            new PlayListItem
            {
                Title = episodeTitle,
                Orig = orig,
                Duration = TimeSpan.FromMinutes(20)
            }
        ];
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}{orig}");
        var html = $"<html><body><span itemprop=\"name\">{collectionTitle}</span></body></html>";
        var sut = _mocker.CreateInstance<MetaDataExtractor>();

        // Act
        var meta = await sut.Extract(url, HtmlResponse(html));

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(collectionTitle);
        meta.ShowName.Should().NotBe(meta.Title);
    }

    private static HttpResponseMessage HtmlResponse(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

    private sealed class StubPlayListProvider(Func<IEnumerable<PlayListItem>> items) : IInternetArchivePlayListProvider
    {
        public IEnumerable<PlayListItem> GetPlayList(HtmlDocument document) => items();
    }
}
