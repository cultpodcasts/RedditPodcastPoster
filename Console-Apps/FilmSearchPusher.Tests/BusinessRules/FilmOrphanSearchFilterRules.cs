using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Search.Models;

namespace FilmSearchPusher.Tests.BusinessRules;

/// <summary>
/// Rules for building the Azure Search orphan filter from Cosmos live film ids (source of truth).
/// </summary>
public class FilmOrphanSearchFilterRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When there are live Cosmos film ids, then the orphan filter is contentKind Film and not search.in those ids, because Cosmos is the source of truth and search only returns the set difference.")]
    public void live_ids_build_not_search_in_filter()
    {
        // Arrange
        var first = _fixture.CreateGuid().ToString();
        var second = _fixture.CreateGuid().ToString();

        // Act
        var built = FilmOrphanSearchFilter.TryBuildNotSearchInFilter([first, second], out var filter);

        // Assert
        built.Should().BeTrue();
        filter.Should().Be(
            $"contentKind eq '{SearchContentKind.Film}' and not search.in(id, '{first},{second}', ',')");
    }

    [Fact(DisplayName =
        "When there are no live Cosmos film ids, then the orphan filter is every Film document, because an empty Cosmos film set means every indexed film document is an orphan.")]
    public void empty_live_ids_select_every_film_document()
    {
        // Arrange
        // Act
        var built = FilmOrphanSearchFilter.TryBuildNotSearchInFilter([], out var filter);

        // Assert
        built.Should().BeTrue();
        filter.Should().Be($"contentKind eq '{SearchContentKind.Film}'");
    }

    [Fact(DisplayName =
        "When live Cosmos film ids would make the not search.in filter longer than the safe OData limit, then the filter is refused so the gateway can fall back to a release-ordered film-id scan.")]
    public void oversized_live_ids_refuse_not_search_in_filter()
    {
        // Arrange
        var liveIds = Enumerable.Range(0, FilmOrphanSearchFilter.MaxFilterLengthChars / 20)
            .Select(_ => _fixture.CreateGuid().ToString())
            .ToArray();

        // Act
        var built = FilmOrphanSearchFilter.TryBuildNotSearchInFilter(liveIds, out var filter);

        // Assert
        built.Should().BeFalse();
        filter.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a live film id contains a comma or quote, then the not search.in filter is refused, because those characters would break the OData search.in list.")]
    public void unsafe_live_id_characters_refuse_not_search_in_filter()
    {
        // Arrange
        var unsafeId = $"{_fixture.CreateGuid()},extra";

        // Act
        var built = FilmOrphanSearchFilter.TryBuildNotSearchInFilter([unsafeId], out var filter);

        // Assert
        built.Should().BeFalse();
        filter.Should().BeNull();
    }

    [Fact(DisplayName =
        "Stable orphan paging orders by release, because id is the document key but is not sortable in the search index schema.")]
    public void stable_page_order_is_release()
    {
        // Arrange
        // Act
        // Assert
        FilmOrphanSearchFilter.StablePageOrderBy.Should().Be("release");
    }
}
