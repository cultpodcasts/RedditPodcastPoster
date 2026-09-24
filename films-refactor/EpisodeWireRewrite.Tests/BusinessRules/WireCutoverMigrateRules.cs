using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace EpisodeWireRewrite.Tests.BusinessRules;

public class WireCutoverMigrateRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "MIGRATE: legacy-only Episode JSON gains publisher*/parent*/releaseSort and drops podcast* denorm keys, " +
        "because GATE 1 must rewrite cold documents the dual-key apps already understand.")]
    public void legacy_only_doc_migrates_to_new_wire_names()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var meta = _fixture.Create<long>();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: search,
            language: language,
            metadataVersion: meta,
            removed: false);
        using var doc = JsonDocument.Parse(json);

        // Act
        var needed = WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change);
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);
        using var afterDoc = JsonDocument.Parse(mut.GetRawText());

        // Assert
        needed.Should().BeTrue();
        afterDoc.RootElement.TryGetProperty("podcastSearchTerms", out _).Should().BeFalse();
        afterDoc.RootElement.TryGetProperty("podcastLanguage", out _).Should().BeFalse();
        afterDoc.RootElement.TryGetProperty("podcastMetadataVersion", out _).Should().BeFalse();
        afterDoc.RootElement.TryGetProperty("podcastRemoved", out _).Should().BeFalse();
        afterDoc.RootElement.GetProperty("publisherSearchTerms").GetString().Should().Be(search);
        afterDoc.RootElement.GetProperty("publisherLanguage").GetString().Should().Be(language);
        afterDoc.RootElement.GetProperty("parentMetadataVersion").GetInt64().Should().Be(meta);
        afterDoc.RootElement.GetProperty("parentRemoved").GetBoolean().Should().BeFalse();
        afterDoc.RootElement.TryGetProperty("releaseSort", out _).Should().BeTrue();
        afterDoc.RootElement.GetProperty("id").GetString().Should().Be(episodeId.ToString());
        afterDoc.RootElement.GetProperty("podcastId").GetString().Should().Be(podcastId.ToString());
    }

    [Fact(DisplayName =
        "MIGRATE: already-new Episode JSON needs no change, because the cutover must be idempotent.")]
    public void already_new_doc_is_unchanged()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var search = _fixture.Create<string>();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            includeReleaseSort: true,
            publisherSearchTerms: search);
        // Strip legacy by building from migrate of a legacy doc first
        using var legacy = JsonDocument.Parse(
            WireCutoverTestJson.LegacyDoc(episodeId, podcastId, searchTerms: search));
        WireCutoverPlanner.TryCreateChange(legacy.RootElement, out var plan).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(legacy.RootElement.GetRawText());
        WireCutoverPlanner.ApplyState(mut, plan!.After);
        using var modern = JsonDocument.Parse(mut.GetRawText());

        // Act
        var needed = WireCutoverPlanner.TryCreateChange(modern.RootElement, out var second);

        // Assert
        needed.Should().BeFalse();
        second.Should().BeNull();
    }

    [Fact(DisplayName =
        "MIGRATE: podcastRemoved true becomes parentRemoved true and legacy key is removed, " +
        "because soft-deleted parents must stay hidden after rewrite.")]
    public void podcastRemoved_true_becomes_parentRemoved_true()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, removed: true);
        using var doc = JsonDocument.Parse(json);

        // Act
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);
        using var after = JsonDocument.Parse(mut.GetRawText());

        // Assert
        after.RootElement.TryGetProperty("podcastRemoved", out _).Should().BeFalse();
        after.RootElement.GetProperty("parentRemoved").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName =
        "MIGRATE: missing releaseSort is derived from release, because Cosmos range filters need releaseSort.")]
    public void missing_releaseSort_is_derived_from_release()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var release = DomainTestFixture.UtcAtTime(-5, new TimeSpan(8, 9, 10));
        var json = WireCutoverTestJson.LegacyDoc(episodeId, podcastId, releaseUtc: release);
        using var doc = JsonDocument.Parse(json);

        // Act
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var sortField = change!.After.Single(f => f.Name == WireCutoverFields.ReleaseSort);

        // Assert
        sortField.Exists.Should().BeTrue();
        sortField.JsonValue.Should().Contain(release.ToString("yyyy-MM-dd"));
    }

    [Fact(DisplayName =
        "MIGRATE: existing releaseSort is left unchanged when present, because migrate must not invent dates.")]
    public void existing_releaseSort_is_preserved()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var release = DomainTestFixture.UtcAtTime(-2, new TimeSpan(1, 2, 3));
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.Create<string>(),
            releaseUtc: release,
            includeReleaseSort: true);
        using var doc = JsonDocument.Parse(json);
        var beforeSort = doc.RootElement.GetProperty("releaseSort").GetRawText();

        // Act
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var afterSort = change!.After.Single(f => f.Name == WireCutoverFields.ReleaseSort).JsonValue;

        // Assert
        afterSort.Should().Be(beforeSort);
    }

    [Fact(DisplayName =
        "MIGRATE: unrelated properties such as description are preserved, because cutover is surgical.")]
    public void unrelated_description_is_preserved()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var description = _fixture.Create<string>();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: _fixture.Create<string>(),
            extraPropertyValue: description);
        using var doc = JsonDocument.Parse(json);

        // Act
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);
        using var after = JsonDocument.Parse(mut.GetRawText());

        // Assert
        after.RootElement.GetProperty("description").GetString().Should().Be(description);
    }

    [Fact(DisplayName =
        "MIGRATE: when publisherSearchTerms already exists, legacy podcastSearchTerms is dropped and publisher kept, " +
        "because new values win over bridges.")]
    public void existing_publisher_wins_over_legacy()
    {
        // Arrange
        var (episodeId, podcastId) = WireCutoverTestJson.NewIds();
        var publisher = _fixture.Create<string>();
        var legacy = _fixture.Create<string>();
        var json = WireCutoverTestJson.LegacyDoc(
            episodeId,
            podcastId,
            searchTerms: legacy,
            publisherSearchTerms: publisher);
        using var doc = JsonDocument.Parse(json);

        // Act
        WireCutoverPlanner.TryCreateChange(doc.RootElement, out var change).Should().BeTrue();
        var mut = JsonObjectMutator.Parse(json);
        WireCutoverPlanner.ApplyState(mut, change!.After);
        using var after = JsonDocument.Parse(mut.GetRawText());

        // Assert
        after.RootElement.GetProperty("publisherSearchTerms").GetString().Should().Be(publisher);
        after.RootElement.TryGetProperty("podcastSearchTerms", out _).Should().BeFalse();
    }
}
