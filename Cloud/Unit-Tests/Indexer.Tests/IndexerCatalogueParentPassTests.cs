using AutoFixture;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Xunit;

namespace Indexer.Tests;

public class IndexerCatalogueParentPassTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "Each indexer pass gets its own slice of the TV and News parent ids, split like podcast ids, because every " +
        "auto-indexed parent must be indexed once per pass cycle.")]
    public void each_pass_gets_its_parent_slice()
    {
        // Arrange
        var parentIds = _fixture.CreateMany<Guid>(9).Order().ToArray();
        var batches = IndexPassBatchSplitter.Split(parentIds, 4);
        var context = new IndexerContext(CatalogueParentIndexIds: batches);

        // Act
        var perPass = Enumerable.Range(1, 4)
            .Select(pass => Activities.Indexer.CatalogueParentIdsForPass(context, pass))
            .ToArray();

        // Assert
        perPass.SelectMany(x => x).Should().Equal(parentIds);
        perPass.Should().OnlyContain(slice => slice.Length > 0);
    }

    [Fact(DisplayName =
        "When the orchestration has no parent batches (a run started before this change), then no parent ids are " +
        "indexed, because older in-flight orchestrations must keep working.")]
    public void missing_parent_batches_mean_no_parents()
    {
        // Arrange
        var context = new IndexerContext();

        // Act
        var ids = Activities.Indexer.CatalogueParentIdsForPass(context, 1);

        // Assert
        ids.Should().BeEmpty();
    }
}
