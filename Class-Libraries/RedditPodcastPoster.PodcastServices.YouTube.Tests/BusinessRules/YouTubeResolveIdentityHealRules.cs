using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes; // pragma: allowlist secret
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Services; // pragma: allowlist secret

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules; // pragma: allowlist secret

/// <summary>
/// Channel submit passes the show's other rows into Resolve. One unrepaired
/// sibling must not fail that show when its YouTube url already names the video.
/// </summary>
public class YouTubeResolveIdentityHealRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SaveCallRecorder _saves = new();
    private readonly InMemoryEpisodeRepository _episodes; // pragma: allowlist secret

    public YouTubeResolveIdentityHealRules()
    {
        _episodes = new InMemoryEpisodeRepository(_saves); // pragma: allowlist secret
        _mocker.Use<IEpisodeRepository>(_episodes); // pragma: allowlist secret
        _mocker.GetMock<IYouTubeChannelReleaseBandSearch>()
            .Setup(x => x.Search(
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new List<SearchResult>());
    }

    [Fact(DisplayName =
        "When a sibling episode has a YouTube url whose video id can be read but no stored id, Resolve fills " +
        "ids.youtube, saves that episode, and keeps searching, because an unsaved fill never reaches search " +
        "and one unrepaired sibling must not fail the podcast.")]
    public async Task sibling_url_with_video_id_is_healed_and_saved()
    {
        // Arrange
        var podcast = PodcastWithChannel();
        var videoId = _fixture.CreateYouTubeId();
        var sibling = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(videoId), null); // pragma: allowlist secret
            EpisodeServicePresence.SetYouTubeIdentity(e, null); // pragma: allowlist secret
        });
        var alreadyConsistent = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _episodes.Seed(sibling, alreadyConsistent); // pragma: allowlist secret
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(
            Criteria(),
            podcast,
            [sibling, alreadyConsistent],
            new IndexingContext());

        // Assert
        result.Should().BeNull();
        EpisodeServicePresence.NestedYouTubeId(sibling).Should().Be(videoId); // pragma: allowlist secret
        _saves.EpisodeCalls.Should().ContainSingle()
            .Which.EpisodeIds.Should().Equal(sibling.Id);
        var saved = await _episodes.GetEpisode(podcast.Id, sibling.Id); // pragma: allowlist secret
        var unchanged = await _episodes.GetEpisode(podcast.Id, alreadyConsistent.Id); // pragma: allowlist secret
        EpisodeServicePresence.NestedYouTubeId(saved!).Should().Be(videoId); // pragma: allowlist secret
        EpisodeServicePresence.NestedYouTubeId(unchanged!).Should() // pragma: allowlist secret
            .Be(EpisodeServicePresence.NestedYouTubeId(alreadyConsistent)); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "When the loaded row's title has diverged from the stored row, Resolve saves the stored title with the " +
        "YouTube id filled, because Save upserts the whole document and the loaded instance is not that document.")]
    public async Task heal_saves_a_fresh_read_not_the_loaded_instance()
    {
        // Arrange
        var podcast = PodcastWithChannel();
        var videoId = _fixture.CreateYouTubeId();
        var storedTitle = _fixture.CreateTitle();
        var loadedTitle = _fixture.CreateTitle();
        storedTitle.Should().NotBe(loadedTitle);
        var sibling = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = storedTitle;
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(videoId), null); // pragma: allowlist secret
            EpisodeServicePresence.SetYouTubeIdentity(e, null); // pragma: allowlist secret
        });
        _episodes.Seed(sibling); // pragma: allowlist secret
        sibling.Title = loadedTitle;
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var result = await sut.Resolve(Criteria(), podcast, [sibling], new IndexingContext());

        // Assert
        result.Should().BeNull();
        sibling.Title.Should().Be(loadedTitle);
        EpisodeServicePresence.NestedYouTubeId(sibling).Should().Be(videoId); // pragma: allowlist secret
        var saved = await _episodes.GetEpisode(podcast.Id, sibling.Id); // pragma: allowlist secret
        saved!.Title.Should().Be(storedTitle);
        EpisodeServicePresence.NestedYouTubeId(saved).Should().Be(videoId); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "When a sibling YouTube url has no video id, Resolve still fails the podcast and does not save, " +
        "because the row cannot be reconciled from the url.")]
    public async Task sibling_url_without_video_id_still_fails_the_podcast()
    {
        // Arrange
        var podcast = PodcastWithChannel();
        var playlistUrl = new Uri($"https://www.youtube.com/playlist?list={_fixture.CreateYouTubePlaylistId()}");
        YouTubeIdResolver.Extract(playlistUrl).Should().BeNull();
        var sibling = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, playlistUrl, null); // pragma: allowlist secret
            EpisodeServicePresence.SetYouTubeIdentity(e, null); // pragma: allowlist secret
        });
        _episodes.Seed(sibling); // pragma: allowlist secret
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var act = () => sut.Resolve(Criteria(), podcast, [sibling], new IndexingContext());

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(sibling.Id.ToString());
        EpisodeServicePresence.NestedYouTubeId(sibling).Should().BeNull(); // pragma: allowlist secret
        _saves.EpisodeCalls.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, sibling.Id))!.Ids?.YouTube.Should().BeNull(); // pragma: allowlist secret
    }

    [Fact(DisplayName =
        "When a sibling's stored YouTube id and url name different videos, Resolve fails the podcast and " +
        "leaves the stored id, because a url must not replace an id that is already set.")]
    public async Task sibling_id_and_url_that_disagree_still_fails_the_podcast()
    {
        // Arrange
        var podcast = PodcastWithChannel();
        var storedId = _fixture.CreateYouTubeId();
        var urlId = _fixture.CreateYouTubeId();
        storedId.Should().NotBe(urlId);
        var sibling = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, storedId); // pragma: allowlist secret
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, _fixture.DefaultYouTubeUrl(urlId), null); // pragma: allowlist secret
        });
        EpisodeServicePresence.NestedYouTubeId(sibling).Should().Be(storedId); // pragma: allowlist secret
        _episodes.Seed(sibling); // pragma: allowlist secret
        var sut = _mocker.CreateInstance<YouTubeUrlCategoriser>();

        // Act
        var act = () => sut.Resolve(Criteria(), podcast, [sibling], new IndexingContext());

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(sibling.Id.ToString());
        var stored = await _episodes.GetEpisode(podcast.Id, sibling.Id); // pragma: allowlist secret
        EpisodeServicePresence.NestedYouTubeId(sibling).Should().Be(storedId); // pragma: allowlist secret
        _saves.EpisodeCalls.Should().BeEmpty();
        EpisodeServicePresence.NestedYouTubeId(stored!).Should().Be(storedId); // pragma: allowlist secret
    }

    private Podcast PodcastWithChannel() =>
        _fixture.CreatePodcast(p => p.YouTubeChannelId = _fixture.CreateYouTubeChannelId());

    private PodcastServiceSearchCriteria Criteria() => // pragma: allowlist secret
        new(
            ShowName: _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: DomainTestFixture.UtcDateDaysAgo(1),
            Duration: _fixture.CreateDuration());
}
