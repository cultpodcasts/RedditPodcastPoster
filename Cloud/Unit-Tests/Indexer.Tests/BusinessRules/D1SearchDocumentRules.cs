using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>SP-2.1 (#1321) / SP-0.3: what a D1 search document carries that the Azure one does not.</summary>
public class D1SearchDocumentRules
{
    // The Api's searchIndexDocSchema keys (workers/search/src/admin-docs.ts): any other key is a 400.
    private static readonly HashSet<string> WorkerKeys =
    [
        "id", "contentKind", "title", "seriesName", "description", "release", "duration", "spotifyId", "appleId",
        "podcastAppleId", "youtubeId", "bbc", "internetArchive", "svc", "subjects", "image", "trailers",
        "alternativeTitles", "imdbId", "tmdbId", "tvdbId", "seriesImdbId", "seriesTmdbId", "seriesTvdbId", "lang",
        "guests", "publisherSearchTerms", "episodeSearchTerms", "modifiedAt"
    ];

    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A D1 podcast-episode document carries the full description, not the Azure ~100-character cut, because " +
        "SP-0.3 indexes full descriptions in D1 from soak day one.")]
    public void episode_document_has_full_description()
    {
        // Arrange
        var description = string.Join(' ', Enumerable.Range(0, 40).Select(_ => _fixture.CreateTitle()));
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateEpisode(e => e.Description = description);

        // Act
        var document = D1SearchDocuments.For(podcast, episode);

        // Assert
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);
        document.Description.Should().Be(description.Trim());
        document.ContentKind.Should().Be(SearchContentKind.Episode);
        document.SeriesName.Should().Be(podcast.Name.Trim());
    }

    [Fact(DisplayName =
        "A D1 document indexes the playable's guests, dropping blank entries, because SP-0.3 pulled guests forward " +
        "to soak day one; with no guests the field is omitted.")]
    public void guests_are_indexed_and_blank_ones_dropped()
    {
        // Arrange
        var guest = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast();
        var withGuests = _fixture.CreateEpisode(e => e.Guests = [$" {guest} ", " "]);
        var withoutGuests = _fixture.CreateEpisode(e => e.Guests = null);

        // Act
        var document = D1SearchDocuments.For(podcast, withGuests);
        var empty = D1SearchDocuments.For(podcast, withoutGuests);

        // Assert
        document.Guests.Should().Equal(guest);
        empty.Guests.Should().BeNull();
    }

    [Fact(DisplayName =
        "modifiedAt is the playable's Cosmos _ts in epoch milliseconds, because the search Worker drops an upsert older " +
        "than the stored document and a replay of the same change must apply as a retry.")]
    public void modified_at_is_cosmos_ts_in_milliseconds()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateEpisode();
        podcast.Timestamp = 0;
        episode.Timestamp = 1_763_400_000;

        // Act
        var document = D1SearchDocuments.For(podcast, episode);

        // Assert
        document.ModifiedAt.Should().Be(1_763_400_000_000);
    }

    [Fact(DisplayName =
        "An episode document's modifiedAt is the later of the episode's and the podcast's Cosmos _ts, because the " +
        "search document denormalises the live podcast and a rename must look stale in D1.")]
    public void episode_modified_at_is_the_later_of_episode_and_podcast_ts()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateEpisode();
        podcast.Timestamp = 1_763_500_000;
        episode.Timestamp = 1_763_400_000;

        // Act
        var document = D1SearchDocuments.For(podcast, episode);

        // Assert
        document.ModifiedAt.Should().Be(1_763_500_000_000);
    }

    [Fact(DisplayName =
        "A D1 film document keeps the Azure film rules (no series, trailers, alternative titles) and adds the full " +
        "description, because the field rules live in the shared push-path builder.")]
    public void film_document_keeps_azure_rules_and_adds_full_description()
    {
        // Arrange
        var aka = _fixture.CreateTitle();
        var description = string.Join(' ', Enumerable.Range(0, 40).Select(_ => _fixture.CreateTitle()));
        var film = new Film(_fixture.CreateTitle()) { Description = description, AlternativeTitles = [aka] };
        film.SetRelease(CatalogueRelease.FromDateTimeUtc(DateTime.UtcNow));

        // Act
        var document = D1SearchDocuments.For(film);

        // Assert
        document.ContentKind.Should().Be(SearchContentKind.Film);
        document.SeriesName.Should().BeNull();
        document.AlternativeTitles.Should().Equal(aka);
        document.Description.Should().Be(description);
    }

    [Fact(DisplayName =
        "TV-episode and news-report D1 documents carry their parent name and the full description, because the D1 " +
        "index holds every playable kind Azure holds.")]
    public void tv_and_news_documents_carry_parent_and_full_description()
    {
        // Arrange
        var description = string.Join(' ', Enumerable.Range(0, 40).Select(_ => _fixture.CreateTitle()));
        var show = _fixture.CreateTitle();
        var organisation = _fixture.CreateTitle();
        var tv = new TvShowEpisode(_fixture.CreateTitle())
            { Id = _fixture.CreateGuid(), TvShowName = show, Description = description };
        var news = new NewsReport(_fixture.CreateTitle())
            { Id = _fixture.CreateGuid(), NewsOrganisationName = organisation, Description = description };

        // Act
        var tvDocument = D1SearchDocuments.For(tv);
        var newsDocument = D1SearchDocuments.For(news);

        // Assert
        tvDocument.ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        tvDocument.SeriesName.Should().Be(show);
        tvDocument.Description.Should().Be(description);
        newsDocument.ContentKind.Should().Be(SearchContentKind.NewsReport);
        newsDocument.SeriesName.Should().Be(organisation);
        newsDocument.Description.Should().Be(description);
    }

    [Fact(DisplayName =
        "The serialised D1 document uses only keys the search Worker accepts and never the legacy Azure names, " +
        "because the Worker answers 400 to any unknown field.")]
    public void serialised_document_uses_only_worker_keys()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateEpisode(e => e.Guests = [_fixture.CreateTitle()]);
        var options = new JsonSerializerOptions
            { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

        // Act
        var json = JsonSerializer.SerializeToElement(D1SearchDocuments.For(podcast, episode), options);

        // Assert
        var keys = json.EnumerateObject().Select(p => p.Name).ToArray();
        keys.Should().OnlyContain(k => WorkerKeys.Contains(k));
        keys.Should().Contain(["id", "release", "duration", "modifiedAt", "guests", "description"]);
    }

    [Fact(DisplayName =
        "D1 eligibility deletes an episode whose denormalised parentRemoved is set, as the Azure episode pull filters " +
        "it, so both indexes hold the same corpus during the soak.")]
    public void eligibility_excludes_parent_removed_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = false);
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Removed = false;
            e.ParentRemoved = true;
        });

        // Act
        var exclude = D1SearchEligibility.ShouldExclude(podcast, episode);

        // Assert
        exclude.Should().BeTrue();
    }
}
