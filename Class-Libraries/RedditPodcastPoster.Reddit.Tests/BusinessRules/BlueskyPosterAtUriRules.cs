using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyPosterAtUriRules
{
    private const string CreatedAtUri = "at://did:plc:example/app.bsky.feed.post/recordkey";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private BlueskyEmbedCardPost? _card;
    private string? _postedUri;
    private string? _language;
    private Episode? _saved;

    public BlueskyPosterAtUriRules()
    {
        _mocker.GetMock<IBlueskyEmbedCardPostFactory>()
            .Setup(factory => factory.Create(It.IsAny<PodcastEpisode>(), It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(() => _card!);
        _mocker.GetMock<IBlueskyFeedClient>()
            .Setup(client => client.PostOpenGraphCard(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>()))
            .Callback<string, Uri, string>((_, _, language) => _language = language)
            .ReturnsAsync(() => _postedUri);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(repository => repository.Save(It.IsAny<Episode>()))
            .Callback<Episode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
    }

    [Fact(DisplayName =
        "A successful Bluesky post stores the returned AT URI on the episode, because a later delete needs that URI.")]
    public async Task successful_post_stores_the_at_uri()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var pair = new PodcastEpisode(podcast, episode);
        _card = new BlueskyEmbedCardPost(episode.Title, _fixture.Create<Uri>(), Service.YouTube);
        _postedUri = CreatedAtUri;
        var sut = _mocker.CreateInstance<BlueskyPoster>();

        // Act
        var status = await sut.Post(pair, shortUrl: null);

        // Assert
        status.Should().Be(BlueskySendStatus.Success);
        _saved.Should().NotBeNull();
        _saved!.BlueskyPost.Should().Be(CreatedAtUri);
        _language.Should().Be("en");
    }

    [Fact(DisplayName =
        "A Bluesky post that is not created leaves the episode unsaved, because there is no AT URI to store.")]
    public async Task failed_post_does_not_save_the_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var pair = new PodcastEpisode(podcast, episode);
        _card = new BlueskyEmbedCardPost(episode.Title, _fixture.Create<Uri>(), Service.YouTube);
        _postedUri = null;
        var sut = _mocker.CreateInstance<BlueskyPoster>();

        // Act
        var status = await sut.Post(pair, shortUrl: null);

        // Assert
        status.Should().Be(BlueskySendStatus.Failure);
        episode.BlueskyPost.Should().BeNull();
        _saved.Should().BeNull();
    }
}
