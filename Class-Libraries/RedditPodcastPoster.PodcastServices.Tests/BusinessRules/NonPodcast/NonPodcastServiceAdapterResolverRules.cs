using FluentAssertions;
using Moq;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Categorisers;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.NonPodcast;

public class NonPodcastServiceAdapterResolverRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly INonPodcastServiceAdapter _bbc;
    private readonly INonPodcastServiceAdapter _archive;
    private readonly NonPodcastServiceAdapterResolver _resolver;

    public NonPodcastServiceAdapterResolverRules()
    {
        _bbc = new BbcNonPodcastServiceAdapter(Mock.Of<IBBCPageMetaDataExtractor>());
        _archive = new InternetArchiveNonPodcastServiceAdapter(
            Mock.Of<IInternetArchivePageMetaDataExtractor>());
        _resolver = new NonPodcastServiceAdapterResolver([_bbc, _archive]);
    }

    [Fact(DisplayName =
        "A BBC Sounds play URL resolves the BBC adapter, so submit routes through that plugin instead of a static host bag.")]
    public void sounds_play_url_resolves_bbc_adapter()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = _resolver.ForSubmit(url);

        // Assert
        adapter.Should().BeSameAs(_bbc);
        _bbc.IsSubmitUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A BBC iPlayer episode URL resolves the BBC adapter, the same submit path as Sounds.")]
    public void iplayer_episode_url_resolves_bbc_adapter()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = _resolver.ForSubmit(url);

        // Assert
        adapter.Should().BeSameAs(_bbc);
    }

    [Fact(DisplayName =
        "A BBC host URL that is not Sounds play or iPlayer episode does not resolve an adapter, " +
        "so news pages are not submitted.")]
    public void bbc_news_path_does_not_resolve_an_adapter()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = _resolver.ForSubmit(url);

        // Assert
        adapter.Should().BeNull();
        _bbc.IsSubmitUrl(url).Should().BeFalse();
        _bbc.CanExtract(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An Internet Archive details URL resolves the Archive adapter.")]
    public void archive_details_url_resolves_archive_adapter()
    {
        // Arrange
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}");

        // Act
        var adapter = _resolver.ForSubmit(url);

        // Assert
        adapter.Should().BeSameAs(_archive);
        _archive.IsSubmitUrl(url).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An archive.org URL that is not a details item does not resolve for submit, " +
        "but extract still matches the host so playlist pages can be walked.")]
    public void archive_search_path_is_not_submit_but_can_extract()
    {
        // Arrange
        var url = new Uri($"https://archive.org/search?query={_fixture.CreateYouTubeId()}");

        // Act
        var submit = _resolver.ForSubmit(url);
        var extract = _resolver.ForExtract(url);

        // Assert
        submit.Should().BeNull();
        extract.Should().BeSameAs(_archive);
        _archive.IsSubmitUrl(url).Should().BeFalse();
        _archive.CanExtract(url).Should().BeTrue();
    }

    [Theory(DisplayName =
        "Vimeo, Netflix, and Prime Video URLs do not resolve BBC or Archive adapters, " +
        "so those plugins must register their own adapters for submit.")]
    [MemberData(nameof(StreamingUrlsThatAreNotBbcOrArchive))]
    public void other_streaming_hosts_do_not_resolve_bbc_or_archive(Uri url)
    {
        // Arrange
        // Act
        var adapter = _resolver.ForSubmit(url);

        // Assert
        adapter.Should().BeNull();
        _bbc.IsSubmitUrl(url).Should().BeFalse();
        _archive.IsSubmitUrl(url).Should().BeFalse();
    }

    public static TheoryData<Uri> StreamingUrlsThatAreNotBbcOrArchive()
    {
        var fixture = new DomainTestFixture();
        return
        [
            new Uri($"https://vimeo.com/{fixture.CreateAppleId()}"),
            new Uri($"https://www.netflix.com/title/{fixture.CreateAppleId()}"),
            new Uri($"https://www.primevideo.com/detail/{fixture.CreateYouTubeId()}")
        ];
    }
}
