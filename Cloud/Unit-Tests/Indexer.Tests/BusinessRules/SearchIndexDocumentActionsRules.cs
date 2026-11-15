using Azure.Search.Documents.Models;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class SearchIndexDocumentActionsRules
{
    [Fact(DisplayName =
        "A Film document is uploaded (replaced), so a null seriesName and podcastName clear the YouTube " +
        "uploader channel that MergeOrUpload would leave in place (#1428).")]
    public void film_document_is_uploaded()
    {
        // Arrange
        var document = Record(SearchContentKind.Film);

        // Act
        var action = SearchIndexDocumentActions.For(document);

        // Assert
        action.ActionType.Should().Be(IndexActionType.Upload);
        action.Document.Should().BeSameAs(document);
    }

    [Theory(DisplayName =
        "A non-film document is merge-or-uploaded, so an omitted field is not wiped.")]
    [InlineData(SearchContentKind.Episode)]
    [InlineData(SearchContentKind.TvShowEpisode)]
    [InlineData(SearchContentKind.NewsReport)]
    public void other_kinds_are_merged_or_uploaded(string contentKind)
    {
        // Arrange
        var document = Record(contentKind);

        // Act
        var action = SearchIndexDocumentActions.For(document);

        // Assert
        action.ActionType.Should().Be(IndexActionType.MergeOrUpload);
        action.Document.Should().BeSameAs(document);
    }

    [Fact(DisplayName =
        "A mixed batch uploads only the Film documents and merge-or-uploads the rest.")]
    public void mixed_batch_uploads_only_films()
    {
        // Arrange
        var film = Record(SearchContentKind.Film);
        var episode = Record(SearchContentKind.Episode);

        // Act
        var batch = SearchIndexDocumentActions.BatchFor([film, episode]);

        // Assert
        batch.Actions.Should().HaveCount(2);
        batch.Actions[0].ActionType.Should().Be(IndexActionType.Upload);
        batch.Actions[0].Document.Should().BeSameAs(film);
        batch.Actions[1].ActionType.Should().Be(IndexActionType.MergeOrUpload);
        batch.Actions[1].Document.Should().BeSameAs(episode);
    }

    private static EpisodeSearchRecord Record(string contentKind) => new()
    {
        Id = Guid.NewGuid().ToString(),
        ContentKind = contentKind,
        Release = DateTimeOffset.UtcNow,
        Duration = "01:00:00",
        BBC = string.Empty,
        InternetArchive = string.Empty,
        Subjects = [],
        PublisherSearchTerms = string.Empty,
        EpisodeSearchTerms = string.Empty
    };
}
