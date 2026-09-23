using FluentAssertions;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueContentTypeModelRulesTests
{
    [Fact(DisplayName =
        "ContentKind enumerates the four playable kinds: Episode, TvShowEpisode, Film, NewsReport.")]
    public void ContentKind_has_expected_playable_values()
    {
        // Arrange
        var names = Enum.GetNames<ContentKind>();

        // Act
        var values = Enum.GetValues<ContentKind>();

        // Assert
        names.Should().BeEquivalentTo(["Episode", "TvShowEpisode", "Film", "NewsReport"]);
        values.Should().HaveCount(4);
        values.Should().Contain(ContentKind.Episode);
        values.Should().Contain(ContentKind.TvShowEpisode);
        values.Should().Contain(ContentKind.Film);
        values.Should().Contain(ContentKind.NewsReport);
    }

    [Fact(DisplayName =
        "Film has no parent id property because a film is a standalone playable with no series parent.")]
    public void Film_has_no_parent_id_property()
    {
        // Arrange
        var filmType = typeof(Film);
        var forbiddenParentIds = new[] { "NewsOrganisationId", "TvShowId", "PodcastId" };

        // Act
        var propertyNames = filmType.GetProperties().Select(p => p.Name).ToArray();

        // Assert
        propertyNames.Should().NotContain(forbiddenParentIds);
    }

    [Fact(DisplayName =
        "Film identity is YouTube-only (YouTubeId): no EpisodeIds bag and no Spotify/Apple identity properties, " +
        "because films are not podcast episodes.")]
    public void Film_uses_youtube_id_not_episode_ids_bag()
    {
        // Arrange
        var filmType = typeof(Film);
        var propertyNames = filmType.GetProperties().Select(p => p.Name).ToArray();

        // Act
        var youtubeId = filmType.GetProperty(nameof(Film.YouTubeId));
        var ids = filmType.GetProperty("Ids");

        // Assert
        youtubeId.Should().NotBeNull();
        youtubeId!.PropertyType.Should().Be(typeof(string));
        ids.Should().BeNull();
        propertyNames.Should().NotContain(["SpotifyId", "AppleId", "Ids"]);
    }

    [Fact(DisplayName =
        "TvShowEpisode.SetTvShowProperties updates TvShowId and TvShowName from the parent show.")]
    public void TvShowEpisode_SetTvShowProperties_updates_id_and_name()
    {
        // Arrange
        var tvShowId = Guid.NewGuid();
        var tvShowName = Guid.NewGuid().ToString("N");
        var tvShow = new TvShow { Id = tvShowId, Name = $" {tvShowName} " };
        var episode = new TvShowEpisode();

        // Act
        var updated = episode.SetTvShowProperties(tvShow);

        // Assert
        updated.Should().BeTrue();
        episode.TvShowId.Should().Be(tvShowId);
        episode.TvShowName.Should().Be(tvShowName);
    }
}
