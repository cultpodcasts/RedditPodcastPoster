using FindDuplicateEpisodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace FindDuplicateEpisodes.Tests;

public class FindDuplicateEpisodesMergeYouTubeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Podcast _podcast;

    public FindDuplicateEpisodesMergeYouTubeRules()
    {
        _podcast = _fixture.CreatePodcast();
    }

    [Fact(DisplayName =
        "Duplicate merge: when the kept episode has services.youtube.url but no ids.youtube, then its id is filled " +
        "from its own url and the duplicate's different video is ignored, because the kept url wins.")]
    public void url_only_target_fills_id_from_its_own_url()
    {
        // Arrange
        var targetVideoId = _fixture.CreateYouTubeId();
        var target = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        target.Services![ServiceKeys.YouTube] = new ServiceLink { Url = _fixture.DefaultYouTubeUrl(targetVideoId) };
        var source = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast);

        // Act
        var merged = FindDuplicateEpisodesProcessor.MergeUrls(target, source);

        // Assert
        merged.Should().BeTrue();
        EpisodeServicePresence.NestedYouTubeId(target).Should().Be(targetVideoId);
        EpisodeServicePresence.TryGetUrl(target, ServiceKeys.YouTube).Should()
            .Be(_fixture.DefaultYouTubeUrl(targetVideoId));
    }

    [Fact(DisplayName =
        "Duplicate merge: when the kept episode has no YouTube and the duplicate has a YouTube url and id, then the " +
        "kept episode takes both, so its id matches its url.")]
    public void target_without_youtube_takes_source_url_and_id()
    {
        // Arrange
        var target = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var source = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast);
        var sourceVideoId = EpisodeServicePresence.NestedYouTubeId(source)!;

        // Act
        var merged = FindDuplicateEpisodesProcessor.MergeUrls(target, source);

        // Assert
        merged.Should().BeTrue();
        EpisodeServicePresence.NestedYouTubeId(target).Should().Be(sourceVideoId);
        EpisodeServicePresence.TryGetUrl(target, ServiceKeys.YouTube).Should()
            .Be(EpisodeServicePresence.TryGetUrl(source, ServiceKeys.YouTube));
    }

    [Fact(DisplayName =
        "Duplicate merge: when the kept episode has no YouTube and the duplicate has ids.youtube but no url, then the " +
        "kept episode takes the id and a watch url for it, so it never has an id without a url.")]
    public void target_taking_source_id_also_gets_a_url()
    {
        // Arrange
        var sourceVideoId = _fixture.CreateYouTubeId();
        var target = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var source = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        EpisodeServicePresence.SetYouTubeIdentity(source, sourceVideoId);

        // Act
        var merged = FindDuplicateEpisodesProcessor.MergeUrls(target, source);

        // Assert
        merged.Should().BeTrue();
        EpisodeServicePresence.NestedYouTubeId(target).Should().Be(sourceVideoId);
        EpisodeServicePresence.TryGetUrl(target, ServiceKeys.YouTube).Should()
            .Be(new Uri($"https://www.youtube.com/watch?v={sourceVideoId}"));
    }
}
