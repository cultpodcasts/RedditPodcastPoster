using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Factories;

/// <summary>
/// FindSpotifyEpisodeRequestFactory must flag MatchOtherServices lookups as expensive when the podcast is unknown.
/// </summary>
public class FindSpotifyEpisodeRequestFactoryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When building a request from search criteria with a null podcast, HasExpensiveSpotifyEpisodesQuery is true " +
        "because an unknown show must not assume a cheap known-id episode listing.")]
    public void Criteria_with_null_podcast_marks_query_expensive()
    {
        // Arrange
        var criteria = CreateCriteria();

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(null, criteria);

        // Assert
        request.HasExpensiveSpotifyEpisodesQuery.Should().BeTrue();
        request.PodcastSpotifyId.Should().BeEmpty();
        request.PodcastName.Should().Be(criteria.ShowName.Trim());
        request.EpisodeTitle.Should().Be(criteria.EpisodeTitle.Trim());
    }

    [Fact(DisplayName =
        "When the podcast has SpotifyEpisodesQueryIsExpensive set, the criteria request carries HasExpensiveSpotifyEpisodesQuery true " +
        "because downstream providers must honour the expensive-query guard.")]
    public void Criteria_with_expensive_podcast_marks_query_expensive()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = true;
        });
        var criteria = CreateCriteria(showName: podcast.Name);

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(podcast, criteria);

        // Assert
        request.HasExpensiveSpotifyEpisodesQuery.Should().BeTrue();
        request.PodcastSpotifyId.Should().Be(podcast.SpotifyId);
    }

    [Fact(DisplayName =
        "When the podcast does not have an expensive Spotify query flag, the criteria request carries HasExpensiveSpotifyEpisodesQuery false " +
        "because known cheap shows may paginate under ReleasedSince.")]
    public void Criteria_with_non_expensive_podcast_does_not_mark_query_expensive()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.SpotifyEpisodesQueryIsExpensive = false;
        });
        var criteria = CreateCriteria(showName: podcast.Name);

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(podcast, criteria);

        // Assert
        request.HasExpensiveSpotifyEpisodesQuery.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When creating a request for a YouTube-discovered episode on a Spotify-primary podcast with a publishing delay, " +
        "Released is adjusted by the delay and EnrichingYouTubeDiscoveredEpisode is true so length matching can run.")]
    public void Create_from_youtube_discovered_episode_adjusts_release_and_flags_enrichment()
    {
        // Arrange
        var delay = TimeSpan.FromHours(9);
        var youTubePublish = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(9));
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.YouTubePublicationOffset = delay.Ticks;
        });
        var length = TimeSpan.FromMinutes(62);
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Title = _fixture.CreateTitle();
            e.Release = youTubePublish;
            e.Length = length;
            e.YouTubeId = youTubeId;
            e.Urls.YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        });

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(podcast, episode);

        // Assert
        request.Released.Should().Be(youTubePublish - delay);
        request.EnrichingYouTubeDiscoveredEpisode.Should().BeTrue();
        request.Length.Should().Be(length);
    }

    [Fact(DisplayName =
        "When creating a direct-id request from a Spotify episode id, EpisodeSpotifyId is set and podcast fields stay empty " +
        "because URL-authority Resolve looks up by episode id without a known show context.")]
    public void Create_from_episode_id_sets_episode_id_with_empty_podcast_fields()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();

        // Act
        var request = FindSpotifyEpisodeRequestFactory.Create(episodeId);

        // Assert
        request.EpisodeSpotifyId.Should().Be(episodeId);
        request.PodcastSpotifyId.Should().BeEmpty();
        request.PodcastName.Should().BeEmpty();
        request.EpisodeTitle.Should().BeEmpty();
        request.Released.Should().BeNull();
        request.HasExpensiveSpotifyEpisodesQuery.Should().BeTrue();
    }

    private PodcastServiceSearchCriteria CreateCriteria(string? showName = null) =>
        new(
            ShowName: showName ?? _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: DomainTestFixture.UtcDateDaysAgo(1),
            Duration: _fixture.CreateDuration());
}
