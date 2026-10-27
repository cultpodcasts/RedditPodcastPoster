using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Factories;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
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

    [Fact(DisplayName =
        "When creating a request for a YouTube-discovered episode on a YouTube-authority podcast with a publishing delay, " +
        "YouTubePublished carries the unshifted YouTube publish and the catalogue lookup keeps it, so the matcher can " +
        "credit Apple audio released the same day as the video when the delay crosses midnight.")]
    public void Create_from_youtube_discovered_episode_carries_unshifted_youtube_publish_to_lookup()
    {
        // Arrange
        var delay = TimeSpan.FromHours(7);
        var youTubePublish = DomainTestFixture.UtcAtTime(-2, new TimeSpan(4, 0, 9));
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.AppleId = _fixture.CreateAppleId();
            p.ReleaseAuthority = Service.YouTube;
            p.YouTubePublicationOffset = delay.Ticks;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateEpisode(e =>
        {
            e.ReleaseUtc = youTubePublish;
            e.YouTubeId = youTubeId;
            e.Urls.YouTube = new Uri($"https://www.youtube.com/watch?v={youTubeId}");
        });

        // Act
        var request = FindAppleEpisodeRequestFactory.Create(podcast, episode);
        var lookup = AppleEpisodeResolver.ToLookup(request, new IndexingContext());

        // Assert
        request.Released.Should().Be(youTubePublish - delay);
        request.YouTubePublished.Should().Be(youTubePublish);
        lookup.YouTubePublished.Should().Be(youTubePublish);
    }

    [Fact(DisplayName =
        "When building a request from YouTube-sourced criteria, YouTubePublished is the unshifted criteria release; " +
        "criteria without a source authority leave it null.")]
    public void Criteria_from_youtube_carry_unshifted_publish_and_others_do_not()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.AppleId = _fixture.CreateAppleId();
            p.YouTubePublicationOffset = TimeSpan.FromHours(7).Ticks;
        });
        var criteria = CreateCriteria(showName: podcast.Name);

        // Act
        var fromYouTube = FindAppleEpisodeRequestFactory.Create(
            podcast, CreateApplePodcast(podcast), criteria with { SourceAuthority = Service.YouTube });
        var unspecified = FindAppleEpisodeRequestFactory.Create(podcast, CreateApplePodcast(podcast), criteria);

        // Assert
        fromYouTube.YouTubePublished.Should().Be(criteria.Release);
        unspecified.YouTubePublished.Should().BeNull();
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
