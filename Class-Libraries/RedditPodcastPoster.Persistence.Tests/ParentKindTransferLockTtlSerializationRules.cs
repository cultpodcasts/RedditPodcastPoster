using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Cosmos activates item TTL only on the JSON system property named exactly <c>ttl</c>. CamelCase naming alone
/// would emit <c>timeToLiveSeconds</c>, which does not expire the marker.
/// </summary>
public class ParentKindTransferLockTtlSerializationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    [Fact(DisplayName =
        "When a kind-transfer marker has TimeToLiveSeconds set, Cosmos STJ options write it as \"ttl\" (not " +
        "timeToLiveSeconds) and round-trip that value, because Cosmos TTL only activates on the system property " +
        "named ttl.")]
    public void marker_ttl_writes_as_ttl_and_round_trips()
    {
        // Arrange
        var staleAfter = TimeSpan.FromMinutes(10);
        var ttlSeconds = ParentKindTransferLock.TimeToLiveSecondsFor(staleAfter);
        var marker = new ParentKindTransferLock
        {
            Id = _fixture.CreateGuid(),
            TargetKind = CatalogueParentKind.TvShow,
            StartedUtc = DomainTestFixture.UtcDaysAgo(0),
            AttemptId = _fixture.CreateGuid(),
            TimeToLiveSeconds = ttlSeconds
        };

        // Act
        var json = JsonSerializer.SerializeToNode(marker, _options)!.AsObject();
        var read = json.Deserialize<ParentKindTransferLock>(_options)!;

        // Assert
        json.ContainsKey("ttl").Should().BeTrue();
        json.ContainsKey("timeToLiveSeconds").Should().BeFalse();
        json["ttl"]!.GetValue<int>().Should().Be(ttlSeconds);
        read.TimeToLiveSeconds.Should().Be(ttlSeconds);
    }

    [Fact(DisplayName =
        "When a kind-transfer marker leaves TimeToLiveSeconds null, Cosmos STJ options omit \"ttl\", because " +
        "WhenWritingNull must not emit ttl: 0 which Cosmos rejects or treats poorly.")]
    public void null_ttl_is_omitted()
    {
        // Arrange
        var marker = new ParentKindTransferLock
        {
            Id = _fixture.CreateGuid(),
            TargetKind = CatalogueParentKind.NewsOrganisation,
            StartedUtc = DomainTestFixture.UtcDaysAgo(1),
            AttemptId = _fixture.CreateGuid()
        };

        // Act
        var json = JsonSerializer.SerializeToNode(marker, _options)!.AsObject();

        // Assert
        marker.TimeToLiveSeconds.Should().BeNull();
        json.ContainsKey("ttl").Should().BeFalse();
        json.ContainsKey("timeToLiveSeconds").Should().BeFalse();
    }
}
