using CreateSearchIndex;
using FluentAssertions;

namespace CreateSearchIndex.Tests;

public class SearchIndexRebuildPlanRules
{
    private const string IndexName = "synthetic-index";

    [Fact(DisplayName =
        "When the index is torn down without --all-playables, then the TV, film and news siblings are torn down and recreated, because the deleted index held their documents and stale high-water marks would never re-push them.")]
    public void teardown_without_all_playables_rebuilds_siblings()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.TearDownSiblings.Should().BeTrue();
        plan.EnsureSiblings.Should().BeTrue();
        plan.ResetSiblings.Should().BeFalse();
        plan.RunSiblings.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a teardown also runs the indexer, then the siblings are run too, because the rebuilt index must get every playable back in the same run.")]
    public void teardown_with_run_indexer_runs_siblings()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true, RunIndexer = true };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.RunSiblings.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When --all-playables is given without a teardown, then the siblings are created or updated but not torn down, because the index and its documents are kept.")]
    public void all_playables_without_teardown_keeps_siblings()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, AllPlayables = true, RunIndexer = true };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.TearDownSiblings.Should().BeFalse();
        plan.EnsureSiblings.Should().BeTrue();
        plan.RunSiblings.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When neither a teardown nor --all-playables is given, then the siblings are left alone, because an episode-only create keeps the existing index.")]
    public void episode_only_run_leaves_siblings_alone()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, RunIndexer = true };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.Should().Be(new SearchIndexRebuildPlan(false, false, false, false));
    }

    [Fact(DisplayName =
        "When --update-existing --reset-indexer --all-playables is given, then the siblings are reset too, because otherwise existing TV, film and news documents never pick up new fields.")]
    public void reset_with_all_playables_resets_siblings()
    {
        // Arrange
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, UpdateExisting = true, ResetIndexer = true, AllPlayables = true
        };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.ResetSiblings.Should().BeTrue();
        plan.EnsureSiblings.Should().BeTrue();
        plan.TearDownSiblings.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When --reset-indexer is given without --all-playables, then the siblings are not reset, because only the named episode indexer was asked for.")]
    public void reset_without_all_playables_leaves_siblings()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, UpdateExisting = true, ResetIndexer = true };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.ResetSiblings.Should().BeFalse();
        plan.EnsureSiblings.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When no index name is given, then no sibling work is planned, because sibling names derive from the index name.")]
    public void no_index_name_plans_nothing()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { TearDownIndex = true, AllPlayables = true, RunIndexer = true };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.Should().Be(new SearchIndexRebuildPlan(false, false, false, false));
    }

    [Fact(DisplayName =
        "When a teardown runs the indexer with --transient-playables, then the siblings are torn down and run one at a time instead of being created together, because a Free-tier service allows only three data sources and three indexers.")]
    public void teardown_with_transient_playables_runs_siblings_one_at_a_time()
    {
        // Arrange
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, TearDownIndex = true, RunIndexer = true, TransientPlayables = true
        };

        // Act
        var plan = SearchIndexRebuildPlan.For(request);

        // Assert
        plan.Should().Be(new SearchIndexRebuildPlan(
            TearDownSiblings: true, EnsureSiblings: false, ResetSiblings: false, RunSiblings: false,
            TransientSiblings: true));
    }

    [Fact(DisplayName =
        "When --transient-playables is given without --run-indexer, then the plan is refused, because a transient sibling is deleted straight after its run and would otherwise never load anything.")]
    public void transient_playables_without_run_indexer_is_refused()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true, TransientPlayables = true };

        // Act
        var act = () => SearchIndexRebuildPlan.For(request);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*--run-indexer*");
    }

    [Fact(DisplayName =
        "When --transient-playables is given with --update-existing, then the plan is refused, because an in-place update keeps scheduled siblings rather than deleting them.")]
    public void transient_playables_with_update_existing_is_refused()
    {
        // Arrange
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, UpdateExisting = true, AllPlayables = true, RunIndexer = true, TransientPlayables = true
        };

        // Act
        var act = () => SearchIndexRebuildPlan.For(request);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*--update-existing*");
    }

    [Fact(DisplayName =
        "When --transient-playables is given without a teardown or --all-playables, then the plan is refused, because there are no siblings to run.")]
    public void transient_playables_without_siblings_is_refused()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, RunIndexer = true, TransientPlayables = true };

        // Act
        var act = () => SearchIndexRebuildPlan.For(request);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
