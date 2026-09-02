using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class PodcastServiceNonPodcastLookupRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PodcastServiceNonPodcastLookupRules()
    {
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        _mocker.Use(NonPodcastSubmitAdapterResolverSupport.Create());
    }

    [Fact(DisplayName =
        "A BBC Sounds play URL does not resolve a series from catalogue show ids, " +
        "because Sounds pages are not a podcast feed — submit creates or uses an explicit series instead.")]
    public async Task sounds_url_does_not_resolve_a_podcast_from_episode_url()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastService>();
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

        // Act
        var podcast = await sut.GetPodcastFromEpisodeUrl(url, new IndexingContext());

        // Assert
        podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "An Internet Archive details URL does not resolve a series from catalogue show ids.")]
    public async Task archive_url_does_not_resolve_a_podcast_from_episode_url()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastService>();
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}");

        // Act
        var podcast = await sut.GetPodcastFromEpisodeUrl(url, new IndexingContext());

        // Assert
        podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "A Vimeo URL does not resolve a series from catalogue show ids, " +
        "because Vimeo pages are not a podcast feed — submit creates or uses an explicit series instead.")]
    public async Task vimeo_url_does_not_resolve_a_podcast_from_episode_url()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastService>();
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");

        // Act
        var podcast = await sut.GetPodcastFromEpisodeUrl(url, new IndexingContext());

        // Assert
        podcast.Should().BeNull();
    }
}
