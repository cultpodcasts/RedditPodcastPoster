using System.Text.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Operations;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Cosmos activates item TTL only on the JSON system property named exactly <c>ttl</c>. CamelCase naming alone
/// would emit <c>timeToLiveSeconds</c>, which does not expire the Activity booking document.
/// </summary>
public class ActivityTtlSerializationRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Fixture _autoFixture = new();
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    [Fact(DisplayName =
        "When Models.Operations.Activity has TimeToLiveSeconds set, Cosmos STJ options write it as \"ttl\" (not " +
        "timeToLiveSeconds) and round-trip that value, because Cosmos TTL only activates on the system property " +
        "named ttl.")]
    public void models_activity_ttl_writes_as_ttl_and_round_trips()
    {
        // Arrange
        var ttlSeconds = PositiveTtlSeconds();
        var activity = new Activity
        {
            Id = _fixture.CreateGuid(),
            Status = _autoFixture.Create<string>(),
            OperationType = _autoFixture.Create<string>(),
            TimeToLiveSeconds = ttlSeconds
        };

        // Act
        var json = JsonSerializer.SerializeToNode(activity, _options)!.AsObject();
        var read = json.Deserialize<Activity>(_options)!;

        // Assert
        json.ContainsKey("ttl").Should().BeTrue();
        json.ContainsKey("timeToLiveSeconds").Should().BeFalse();
        json["ttl"]!.GetValue<int>().Should().Be(ttlSeconds);
        read.TimeToLiveSeconds.Should().Be(ttlSeconds);
    }

    [Fact(DisplayName =
        "When Models.Operations.Activity leaves TimeToLiveSeconds null, Cosmos STJ options omit \"ttl\", because " +
        "WhenWritingNull must not emit ttl: 0 which Cosmos rejects or treats poorly.")]
    public void models_activity_null_ttl_is_omitted()
    {
        // Arrange
        var activity = new Activity
        {
            Id = _fixture.CreateGuid(),
            Status = _autoFixture.Create<string>(),
            OperationType = _autoFixture.Create<string>()
        };

        // Act
        var json = JsonSerializer.SerializeToNode(activity, _options)!.AsObject();

        // Assert
        activity.TimeToLiveSeconds.Should().BeNull();
        json.ContainsKey("ttl").Should().BeFalse();
        json.ContainsKey("timeToLiveSeconds").Should().BeFalse();
    }

    [Fact(DisplayName =
        "When Azure.Activity has TimeToLiveSeconds set, Cosmos STJ options write it as \"ttl\" (not " +
        "timeToLiveSeconds) and round-trip that value, because ActivityMarshaller deserialises sproc responses " +
        "into that type and Cosmos TTL only activates on the system property named ttl.")]
    public void azure_activity_ttl_writes_as_ttl_and_round_trips()
    {
        // Arrange
        var ttlSeconds = PositiveTtlSeconds();
        var activity = new global::Azure.Activity
        {
            Id = _fixture.CreateGuid(),
            Status = _autoFixture.Create<string>(),
            OperationType = _autoFixture.Create<string>(),
            TimeToLiveSeconds = ttlSeconds
        };

        // Act
        var json = JsonSerializer.SerializeToNode(activity, _options)!.AsObject();
        var read = json.Deserialize<global::Azure.Activity>(_options)!;

        // Assert
        json.ContainsKey("ttl").Should().BeTrue();
        json.ContainsKey("timeToLiveSeconds").Should().BeFalse();
        json["ttl"]!.GetValue<int>().Should().Be(ttlSeconds);
        read.TimeToLiveSeconds.Should().Be(ttlSeconds);
    }

    [Fact(DisplayName =
        "When Azure.Activity leaves TimeToLiveSeconds null, Cosmos STJ options omit \"ttl\", because " +
        "WhenWritingNull must not emit ttl: 0 which Cosmos rejects or treats poorly.")]
    public void azure_activity_null_ttl_is_omitted()
    {
        // Arrange
        var activity = new global::Azure.Activity
        {
            Id = _fixture.CreateGuid(),
            Status = _autoFixture.Create<string>(),
            OperationType = _autoFixture.Create<string>()
        };

        // Act
        var json = JsonSerializer.SerializeToNode(activity, _options)!.AsObject();

        // Assert
        activity.TimeToLiveSeconds.Should().BeNull();
        json.ContainsKey("ttl").Should().BeFalse();
        json.ContainsKey("timeToLiveSeconds").Should().BeFalse();
    }

    private int PositiveTtlSeconds() => Math.Abs(_autoFixture.Create<int>()) + 1;
}
