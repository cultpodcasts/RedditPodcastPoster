using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace EpisodeWireRewrite.Tests.BusinessRules;

/// <summary>
/// Rollback rules outnumber migrate rules on purpose — GATE 1 rollback must be
/// the better-tested path when operators need to reverse a bad apply.
/// </summary>
public class WireCutoverRollbackRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "ROLLBACK: restoring Before after migrate returns legacy podcast* keys and removes publisher* when they were absent, " +
        "because rollback must reverse the cutover field set exactly.")]
    public void rollback_restores_legacy_keys_from_before_snapshot()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.CreateGuid().ToString("N");
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: search);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);

        // Act
        WireCutoverPlanner.ApplyState(mut, WireCutoverPlanner.RollbackTarget(change));
        using var rolled = JsonDocument.Parse(mut.GetRawText());

        // Assert
        rolled.RootElement.GetProperty("podcastSearchTerms").GetString().Should().Be(search);
        rolled.RootElement.TryGetProperty("publisherSearchTerms", out _).Should().BeFalse();
        rolled.RootElement.TryGetProperty("releaseSort", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "ROLLBACK: parentRemoved true documents return to podcastRemoved true only, " +
        "because hidden parents must stay hidden after rollback.")]
    public void rollback_restores_podcastRemoved_true()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, removed: true);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);
        WireCutoverPlanner.ApplyState(mut, WireCutoverPlanner.RollbackTarget(change));
        using var rolled = JsonDocument.Parse(mut.GetRawText());

        // Assert
        rolled.RootElement.GetProperty("podcastRemoved").GetBoolean().Should().BeTrue();
        rolled.RootElement.TryGetProperty("parentRemoved", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "ROLLBACK: releaseSort added by migrate is removed when Before lacked it, " +
        "because rollback must not leave additive keys behind.")]
    public void rollback_removes_added_releaseSort()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: _fixture.CreateGuid().ToString("N"));
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);
        mut.TryGet("releaseSort", out _).Should().BeTrue();

        // Act
        WireCutoverPlanner.ApplyState(mut, WireCutoverPlanner.RollbackTarget(change));

        // Assert
        mut.TryGet("releaseSort", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "ROLLBACK: pre-existing releaseSort is restored to the same raw value, " +
        "because rollback must not alter dates that were already present.")]
    public void rollback_preserves_preexisting_releaseSort_value()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.CreateGuid().ToString("N"),
            includeReleaseSort: true);
        using var doc = JsonDocument.Parse(json);
        var originalSort = doc.RootElement.GetProperty("releaseSort").GetRawText();
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);

        // Act
        WireCutoverPlanner.ApplyState(mut, WireCutoverPlanner.RollbackTarget(change));
        using var rolled = JsonDocument.Parse(mut.GetRawText());

        // Assert
        rolled.RootElement.GetProperty("releaseSort").GetRawText().Should().Be(originalSort);
    }

    [Fact(DisplayName =
        "ROLLBACK: description and podcastId/podcastName identity fields are untouched, " +
        "because rollback is as surgical as migrate.")]
    public void rollback_does_not_touch_unrelated_or_identity_fields()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var description = _fixture.CreateGuid().ToString("N");
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.CreateGuid().ToString("N"),
            extraPropertyValue: description);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);

        // Act
        WireCutoverPlanner.ApplyState(mut, WireCutoverPlanner.RollbackTarget(change));
        using var rolled = JsonDocument.Parse(mut.GetRawText());

        // Assert
        rolled.RootElement.GetProperty("description").GetString().Should().Be(description);
        rolled.RootElement.GetProperty("id").GetString().Should().Be(episodeId.ToString());
        rolled.RootElement.GetProperty("podcastId").GetString().Should().Be(podcastId.ToString());
    }

    [Fact(DisplayName =
        "ROLLBACK: RollbackTarget equals the migrate Before snapshot byte-for-byte on each field, " +
        "because rollback must not re-derive intent.")]
    public void rollback_target_is_exact_before_snapshot()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.CreateGuid().ToString("N"),
            language: _fixture.CreateGuid().ToString("N"),
            removed: true);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();

        // Act
        var target = WireCutoverPlanner.RollbackTarget(change!);

        // Assert
        WireCutoverPlanner.StatesEqual(target, change!.Before).Should().BeTrue();
    }

    [Fact(DisplayName =
        "ROLLBACK: patch delta from After to Before is non-empty after a real migrate, " +
        "because Cosmos rollback must emit reverse operations.")]
    public void rollback_patch_delta_is_non_empty_after_migrate()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.CreateGuid().ToString("N"),
            removed: true);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();

        // Act
        var ops = WireCutoverPatchBuilder.BuildDelta(change!.After, change.Before);

        // Assert
        ops.Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "ROLLBACK: patch delta is empty when live state already matches Before, " +
        "because idempotent rollback must not spam Cosmos.")]
    public void rollback_patch_delta_empty_when_already_before()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.CreateGuid().ToString("N"));
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();

        // Act
        var ops = WireCutoverPatchBuilder.BuildDelta(change!.Before, change.Before);

        // Assert
        ops.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "ROLLBACK: migrate then rollback then migrate again produces the same After state, " +
        "because operators may re-apply after a clean rollback.")]
    public void remigrate_after_rollback_matches_first_after()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.CreateGuid().ToString("N"),
            language: _fixture.CreateGuid().ToString("N"),
            metadataVersion: Math.Abs(_fixture.CreateGuid().GetHashCode()),
            removed: false);
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var first).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, first!.After);
        WireCutoverPlanner.ApplyState(mut, WireCutoverPlanner.RollbackTarget(first));
        using var rolled = JsonDocument.Parse(mut.GetRawText());

        // Act
        WireCutoverPlanner.TryCreateChange(rolled.RootElement, out var second).Should().BeTrue();

        // Assert
        WireCutoverPlanner.StatesEqual(first.After, second!.After).Should().BeTrue();
    }

    [Fact(DisplayName =
        "ROLLBACK: journal entry ToChange preserves EpisodeId/PodcastId for collectable entity identity, " +
        "because operators must map every restored document to an id.")]
    public void journal_entry_preserves_entity_ids()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: _fixture.CreateGuid().ToString("N"));
        using var doc = JsonDocument.Parse(json);
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var entry = new WireCutoverJournalEntry
        {
            EpisodeId = change!.EpisodeId,
            PodcastId = change.PodcastId,
            Applied = true,
            Reason = change.Reason,
            Before = change.Before,
            After = change.After,
            FieldChanges = change.FieldChanges,
            RecordedUtc = DateTime.UtcNow
        };

        // Act
        var roundTrip = entry.ToChange();

        // Assert
        roundTrip.EpisodeId.Should().Be(episodeId);
        roundTrip.PodcastId.Should().Be(podcastId);
    }
}
