using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules;

public class SubmitUrlEpisodeIdModeValidatorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When --episode-id is used without -r / --refresh-meta, EnsureValid throws " +
        "because overwrite of release/duration/title must be explicit.")]
    public void missing_refresh_meta_throws()
    {
        // Arrange
        // Act
        var act = () => SubmitUrlEpisodeIdModeValidator.EnsureValid(
            refreshMeta: false,
            submitUrlsInFile: false,
            isInternetArchivePlaylist: false,
            createPodcast: false,
            urlOrFile: null);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*--episode-id requires -r*");
    }

    [Fact(DisplayName =
        "When --episode-id is combined with a positional url/file, EnsureValid throws " +
        "because episode-id mode resolves URLs from stored services only.")]
    public void combined_url_or_file_throws()
    {
        // Arrange
        var urlOrFile = $"https://example.test/{_fixture.CreateYouTubeId()}";

        // Act
        var act = () => SubmitUrlEpisodeIdModeValidator.EnsureValid(
            refreshMeta: true,
            submitUrlsInFile: false,
            isInternetArchivePlaylist: false,
            createPodcast: false,
            urlOrFile);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be combined*");
    }

    [Fact(DisplayName =
        "When --episode-id is combined with -f / -l / -c, EnsureValid throws " +
        "because those flags belong to url/file ingest paths.")]
    public void combined_file_playlist_or_create_throws()
    {
        // Arrange
        // Act
        var withFile = () => SubmitUrlEpisodeIdModeValidator.EnsureValid(
            refreshMeta: true,
            submitUrlsInFile: true,
            isInternetArchivePlaylist: false,
            createPodcast: false,
            urlOrFile: null);
        var withPlaylist = () => SubmitUrlEpisodeIdModeValidator.EnsureValid(
            refreshMeta: true,
            submitUrlsInFile: false,
            isInternetArchivePlaylist: true,
            createPodcast: false,
            urlOrFile: null);
        var withCreate = () => SubmitUrlEpisodeIdModeValidator.EnsureValid(
            refreshMeta: true,
            submitUrlsInFile: false,
            isInternetArchivePlaylist: false,
            createPodcast: true,
            urlOrFile: null);

        // Assert
        withFile.Should().Throw<InvalidOperationException>().WithMessage("*cannot be combined*");
        withPlaylist.Should().Throw<InvalidOperationException>().WithMessage("*cannot be combined*");
        withCreate.Should().Throw<InvalidOperationException>().WithMessage("*cannot be combined*");
    }

    [Fact(DisplayName =
        "When --episode-id is used with -r and without url/file/-f/-l/-c, EnsureValid succeeds " +
        "so Process can resolve catalogue URLs via SubmitUrlStreamingUrlResolver.")]
    public void valid_episode_id_mode_does_not_throw()
    {
        // Arrange
        // Act
        var act = () => SubmitUrlEpisodeIdModeValidator.EnsureValid(
            refreshMeta: true,
            submitUrlsInFile: false,
            isInternetArchivePlaylist: false,
            createPodcast: false,
            urlOrFile: null);

        // Assert
        act.Should().NotThrow();
    }
}
