using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class CatalogueMigrateSearchDocumentsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A News search-swap document keeps the episode id as the search key and sets contentKind NewsReport.")]
    public void news_swap_keeps_episode_id_and_sets_kind()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
            podcast,
            [episode],
            SearchContentKind.NewsReport);

        // Assert
        documents.Should().ContainSingle();
        documents[0].Id.Should().Be(episode.Id.ToString());
        documents[0].ContentKind.Should().Be(SearchContentKind.NewsReport);
        documents[0].SeriesName.Should().Be(podcast.Name.Trim());
    }

    [Fact(DisplayName =
        "A TV search-swap document keeps the episode id and sets contentKind TvShowEpisode.")]
    public void tv_swap_keeps_episode_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
            podcast,
            [episode],
            SearchContentKind.TvShowEpisode);

        // Assert
        documents.Should().ContainSingle();
        documents[0].Id.Should().Be(episode.Id.ToString());
        documents[0].ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        documents[0].SeriesName.Should().Be(CatalogueTvShowCanonicalNames.ShowNameFor(podcast.Name));
    }

    [Fact(DisplayName =
        "A TV search-swap document for a curator programme uses the canonical show name as seriesName, " +
        "because the public TV page is the show, not the channel the videos were filed under.")]
    public void tv_swap_uses_canonical_show_name()
    {
        // Arrange
        var publisherName = CatalogueTvShowCanonicalNames.PublisherNames.First();
        var podcast = _fixture.CreatePodcast(p => p.Name = publisherName);
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
            podcast,
            [episode],
            SearchContentKind.TvShowEpisode);

        // Assert
        documents.Should().ContainSingle();
        documents[0].SeriesName.Should().Be(CatalogueTvShowCanonicalNames.ShowNameFor(publisherName));
        documents[0].ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
    }

    [Fact(DisplayName =
        "A Film search-swap document uses the episode id as the search key, sets contentKind Film, " +
        "clears seriesName because Film has no parent, caps description at the search size, " +
        "and omits seriesDescription from JSON.")]
    public void film_swap_uses_episode_id_and_has_no_series()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        episode.Description = new string('a', Constants.DescriptionSize - 10) + " extra words beyond the search cap";

        // Act
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
            podcast,
            [episode],
            SearchContentKind.Film);

        // Assert
        documents.Should().ContainSingle();
        documents[0].Id.Should().Be(episode.Id.ToString());
        documents[0].ContentKind.Should().Be(SearchContentKind.Film);
        documents[0].SeriesName.Should().BeNull();
        documents[0].Title.Should().Be(episode.Title.Trim());
        documents[0].Description.Should().Be(DescriptionTruncator.TruncateForSearch(episode.Description));
        documents[0].Description!.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        JsonSerializer.Serialize(documents[0]).Should().NotContain("seriesDescription");
    }

    [Fact(DisplayName =
        "Search-swap documents stay Episode-shaped when no episodes are supplied, " +
        "so an empty migrate plan uploads nothing.")]
    public void empty_episodes_yield_no_documents()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();

        // Act
        var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
            podcast,
            [],
            SearchContentKind.NewsReport);

        // Assert
        documents.Should().BeEmpty();
    }
}
