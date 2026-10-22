using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

/// <summary>
/// How YouTube indexing hints are stored: <c>youTubeIndexingHints</c> on a podcast and
/// <c>indexing.youTubeIndexingHints</c> on a TV show or news organisation, omitted when null so no migration is needed.
/// </summary>
public class YouTubeIndexingHintsPersistenceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();
    private readonly YouTubeIndexingHints _hints;

    public YouTubeIndexingHintsPersistenceRules()
    {
        _hints = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            LastFullWalk = DomainTestFixture.UtcDaysAgo(1),
            PlaylistItemCount = 321,
            UploadsCheckedThrough = DomainTestFixture.UtcDaysAgo(0),
            UploadsCheckedThroughVideoId = _fixture.CreateYouTubeId()
        };
    }

    private Podcast PodcastWithHints()
    {
        var podcast = _fixture.CreatePodcast(p => p.YouTubePlaylistId = _hints.PlaylistId!);
        podcast.YouTubeIndexingHints = _hints.Copy();
        return podcast;
    }

    [Fact(DisplayName =
        "A podcast, TV show or news organisation without hints writes no youTubeIndexingHints member, because " +
        "documents saved before hints existed must read and write unchanged.")]
    public void null_hints_are_omitted()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var tvShow = CatalogueParentKindMapper.ToTvShow(podcast);

        // Act
        var podcastJson = JsonSerializer.SerializeToNode(podcast, _options)!.AsObject();
        var indexingJson = JsonSerializer.SerializeToNode(tvShow, _options)!["indexing"]!.AsObject();

        // Assert
        podcastJson.ContainsKey("youTubeIndexingHints").Should().BeFalse();
        podcastJson.ContainsKey("youTubeIndexing").Should().BeFalse();
        indexingJson.ContainsKey("youTubeIndexingHints").Should().BeFalse();
    }

    [Fact(DisplayName =
        "Hints round-trip on a podcast and inside a TV show's indexing block, with null hint members omitted.")]
    public void hints_round_trip()
    {
        // Arrange
        var podcast = PodcastWithHints();
        podcast.YouTubeIndexingHints!.UploadsCheckedThroughVideoId = null;
        var tvShow = CatalogueParentKindMapper.ToTvShow(podcast);

        // Act
        var podcastJson = JsonSerializer.Serialize(podcast, _options);
        var tvShowJson = JsonSerializer.Serialize(tvShow, _options);
        var podcastBack = JsonSerializer.Deserialize<Podcast>(podcastJson, _options)!;
        var tvShowBack = JsonSerializer.Deserialize<TvShow>(tvShowJson, _options)!;

        // Assert
        podcastBack.YouTubeIndexingHints.Should().Be(podcast.YouTubeIndexingHints);
        tvShowBack.Indexing!.YouTubeIndexingHints.Should().Be(podcast.YouTubeIndexingHints);
        JsonNode.Parse(podcastJson)!["youTubeIndexingHints"]!.AsObject()
            .ContainsKey("uploadsCheckedThroughVideoId").Should().BeFalse();
        JsonNode.Parse(tvShowJson)!["indexing"]!["youTubeIndexingHints"]!["playlistItemCount"]!.GetValue<int>()
            .Should().Be(321);
    }

    [Fact(DisplayName =
        "The indexer patch for changed hints sets /indexing/youTubeIndexingHints to the whole hints object.")]
    public void patch_sets_the_hints_path()
    {
        // Arrange
        var indexing = CatalogueIndexingSource.FromPodcast(PodcastWithHints());
        var patch = new CatalogueParentIndexerPatch(null, null, indexing, ["youTubeIndexingHints"]);

        // Act
        var operations = CatalogueParentIndexerPatchOperations.Build(patch, _options).SelectMany(x => x).ToList();

        // Assert
        operations.Select(x => x.Path).Should().Equal("/indexing/youTubeIndexingHints");
    }

    [Fact(DisplayName =
        "The parent indexer's transient podcast and CaptureFrom carry the hints as detached copies, because discovery " +
        "runs on the transient podcast and writes its hints back to the parent's indexing block.")]
    public void transient_podcast_and_capture_keep_hints()
    {
        // Arrange
        var podcast = PodcastWithHints();
        var tvShow = CatalogueParentKindMapper.ToTvShow(podcast);
        var transient = tvShow.Indexing!.ToTransientPodcast(tvShow);
        var updated = transient.YouTubeIndexingHints!.Copy();
        updated.PlaylistItemCount = 400;
        transient.YouTubeIndexingHints = updated;

        // Act
        tvShow.Indexing.CaptureFrom(transient);

        // Assert
        tvShow.Indexing.YouTubeIndexingHints.Should().Be(updated);
        tvShow.Indexing.YouTubeIndexingHints.Should().NotBeSameAs(updated);
        tvShow.YouTubeIndexing.Should().BeSameAs(tvShow.Indexing);
        podcast.YouTubeIndexing.Should().BeSameAs(podcast);
    }
}
