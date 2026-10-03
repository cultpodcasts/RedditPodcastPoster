using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using Azure.Search.Documents;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Twitter.Managers;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services;

public class EpisodeUpdateServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastEpisodeResolverResponse _resolved =
        new(null, null, PodcastEpisodeResolveState.PodcastNotFound);
    private Episode? _saved;
    private string? _uriSeenByRemove;
    private Func<Task<EntitySearchIndexerResponse>> _index = () =>
        Task.FromResult(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });

    public EpisodeUpdateServiceTests()
    {
        _mocker.Use(new EpisodeChangeApplier(NullLogger<EpisodeChangeApplier>.Instance));
        _mocker.Use(new EpisodeSearchIndexCleanup(
            CreateUninitializedSearchClient(),
            NullLogger<EpisodeSearchIndexCleanup>.Instance));
        _mocker.Use(NullLogger<EpisodeUpdateService>.Instance);
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(() => _resolved);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Callback<Episode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisode(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .Returns((Podcast _, Episode _, CancellationToken _) => _index());
    }

    [Fact(DisplayName =
        "Plain English rule: when the episode is not found, then return NotFound and do not save, because there is nothing to update.")]
    public async Task update_returns_not_found_and_does_not_save_when_episode_missing()
    {
        // Arrange
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                _fixture.CreateGuid(),
                _fixture.CreateGuid(),
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast is not found for a resolved episode, then return NotFound and do not save, because the episode cannot be updated without its podcast.")]
    public async Task update_returns_not_found_and_does_not_save_when_podcast_missing()
    {
        // Arrange
        var episode = OldEpisode();
        _resolved = new PodcastEpisodeResolverResponse(episode, null, PodcastEpisodeResolveState.Resolved);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                episode.PodcastId,
                episode.Id,
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when a title-only change is accepted, then save the episode once, because persistence is the core update outcome.")]
    public async Task update_happy_path_saves_episode_once()
    {
        // Arrange
        var (episode, podcast) = ResolvedOldPair();
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.Should().Be(episode);
    }

    [Fact(DisplayName =
        "Plain English rule: when the change request does not untweet or unbluesky, then social remove managers are not called, because those side effects are flag-driven.")]
    public async Task update_without_unsocial_flags_does_not_call_social_managers()
    {
        // Arrange
        var (episode, podcast) = ResolvedOldPair(e =>
        {
            e.Tweeted = true;
            e.BlueskyPost = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        });
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { Title = _fixture.CreateTitle() }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _mocker.GetMock<ITweetManager>().Verify(m => m.RemoveTweet(It.IsAny<PodcastEpisode>()), Times.Never);
        _mocker.GetMock<IBlueskyPostManager>().Verify(m => m.RemovePost(It.IsAny<PodcastEpisode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when UnBluesky succeeds, then RemovePost sees the AT URI on the episode and Cosmostate is cleared before Save, because delete-before-clear keeps retry possible on failure.")]
    public async Task update_unbluesky_deletes_then_clears_on_success()
    {
        // Arrange
        const string atUri = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        var (episode, podcast) = ResolvedOldPair(e => e.BlueskyPost = atUri);
        _mocker.GetMock<IBlueskyPostManager>()
            .Setup(m => m.RemovePost(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(pe => _uriSeenByRemove = pe.Episode.BlueskyPost)
            .ReturnsAsync(RemovePostState.Deleted);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { UnBluesky = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.BlueskyPostDeleted.Should().BeTrue();
        _uriSeenByRemove.Should().Be(atUri);
        episode.BlueskyPost.Should().BeNull();
        _mocker.GetMock<IBlueskyPostManager>().Verify(m => m.RemovePost(It.IsAny<PodcastEpisode>()), Times.Once);
    }

    [Fact(DisplayName =
        "Plain English rule: when UnBluesky delete fails, then Cosmostate keeps BlueskyPost, because a failed remote delete must not flip posted state and block retries.")]
    public async Task update_unbluesky_keeps_at_uri_when_delete_fails()
    {
        // Arrange
        const string atUri = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";
        var (episode, podcast) = ResolvedOldPair(e => e.BlueskyPost = atUri);
        _mocker.GetMock<IBlueskyPostManager>()
            .Setup(m => m.RemovePost(It.IsAny<PodcastEpisode>()))
            .ReturnsAsync(RemovePostState.Other);
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { UnBluesky = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        result.Outcome!.BlueskyPostDeleted.Should().BeFalse();
        episode.BlueskyPost.Should().Be(atUri);
        episode.BlueskyPosted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When search indexing throws after Cosmos save, then the update is still Accepted, because a curator guests POST must not 500 after the document is persisted.")]
    public async Task update_accepted_when_indexer_throws_after_save()
    {
        // Arrange
        var guest = _fixture.CreateTitle();
        var (episode, podcast) = ResolvedOldPair();
        _index = () => throw new InvalidOperationException("search unavailable");
        var sut = _mocker.CreateInstance<EpisodeUpdateService>();

        // Act
        var result = await sut.UpdateAsync(
            new EpisodeChangeRequestWrapper(
                podcast.Id,
                episode.Id,
                new EpisodeChangeRequest { Guests = [guest] }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.Should().Be(episode);
        episode.Guests.Should().Equal(guest);
    }

    private (Episode Episode, Podcast Podcast) ResolvedOldPair(Action<Episode>? customize = null)
    {
        var episode = OldEpisode(customize);
        var podcast = _fixture.CreatePodcast(p => p.Id = episode.PodcastId);
        _resolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        return (episode, podcast);
    }

    private Episode OldEpisode(Action<Episode>? customize = null) =>
        _fixture.CreateEpisode(e =>
        {
            e.ReleaseUtc = DateTime.UtcNow.AddDays(-30);
            customize?.Invoke(e);
        });

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
