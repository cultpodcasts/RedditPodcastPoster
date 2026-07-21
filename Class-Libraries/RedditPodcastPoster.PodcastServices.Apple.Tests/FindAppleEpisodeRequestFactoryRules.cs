using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Factories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

/// <summary>
/// Criteria-based Apple episode requests must honour the podcast's YouTube publishing delay when the
/// criteria originated from a YouTube URL (Jul 2026 incident: a submitted YouTube video attached the
/// same-day Apple episode instead of the delay-shifted true counterpart).
/// </summary>
public class FindAppleEpisodeRequestFactoryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When building a request from YouTube-sourced criteria on an audio-primary podcast with a positive " +
        "publishing delay, Released is shifted back by the delay and EnrichingYouTubeDiscoveredEpisode is true " +
        "so the Apple lookup anchors on the expected audio day, not the YouTube publish day.")]
    public void Criteria_from_youtube_url_shifts_release_by_delay_and_flags_enrichment()
    {
        // Arrange
        var delay = TimeSpan.FromDays(7);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.AppleId = _fixture.CreateAppleId();
            p.YouTubePublicationOffset = delay.Ticks;
        });
        var criteria = CreateCriteria(showName: podcast.Name) with { SourceAuthority = Service.YouTube };

        // Act
        var request = FindAppleEpisodeRequestFactory.Create(podcast, CreateApplePodcast(podcast), criteria);

        // Assert
        request.Released.Should().Be(criteria.Release - delay);
        request.EnrichingYouTubeDiscoveredEpisode.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When building a request from criteria without a source authority on the same delayed podcast, " +
        "Released stays unshifted and EnrichingYouTubeDiscoveredEpisode is false " +
        "because only YouTube-sourced criteria imply a delayed audio counterpart.")]
    public void Criteria_without_source_authority_does_not_shift_release()
    {
        // Arrange
        var delay = TimeSpan.FromDays(7);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.AppleId = _fixture.CreateAppleId();
            p.YouTubePublicationOffset = delay.Ticks;
        });
        var criteria = CreateCriteria(showName: podcast.Name);

        // Act
        var request = FindAppleEpisodeRequestFactory.Create(podcast, CreateApplePodcast(podcast), criteria);

        // Assert
        request.Released.Should().Be(criteria.Release);
        request.EnrichingYouTubeDiscoveredEpisode.Should().BeFalse();
    }

    private static iTunesSearch.Library.Models.Podcast CreateApplePodcast(Podcast podcast) =>
        new()
        {
            Id = podcast.AppleId!.Value,
            Name = podcast.Name
        };

    private PodcastServiceSearchCriteria CreateCriteria(string? showName = null) =>
        new(
            ShowName: showName ?? _fixture.CreateTitle(),
            ShowDescription: _fixture.CreateTitle(),
            Publisher: _fixture.CreateTitle(),
            EpisodeTitle: _fixture.CreateTitle(),
            EpisodeDescription: _fixture.CreateTitle(),
            Release: DomainTestFixture.UtcAtTime(-1, new TimeSpan(13, 48, 0)),
            Duration: _fixture.CreateDuration());
}
