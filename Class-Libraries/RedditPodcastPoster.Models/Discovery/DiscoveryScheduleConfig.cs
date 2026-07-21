using System.Text.Json.Serialization;

using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Discovery;

/// <summary>
/// Cosmos LookUps singleton controlling when <c>discover-infra</c> runs.
/// Timer wakes every 30 minutes; runs only when UK local time matches <see cref="RunTimes"/>.
/// </summary>
[CosmosSelector(ModelType.DiscoveryScheduleConfig)]
public sealed class DiscoveryScheduleConfig : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

    public const string DefaultTimeZoneId = "GMT Standard Time";

    /// <summary>Default UK run times when the LookUps document is missing.</summary>
    public static readonly IReadOnlyList<string> DefaultRunTimes = ["08:00", "22:00"];

    public DiscoveryScheduleConfig()
    {
        Id = _Id;
        ModelType = ModelType.DiscoveryScheduleConfig;
    }

    /// <summary>
    /// UK local times on a 30-minute grid (HH:mm), e.g. <c>08:00</c>, <c>22:00</c>.
    /// </summary>
    [JsonPropertyName("runTimes")]
    [JsonPropertyOrder(10)]
    public List<string> RunTimes { get; set; } = [.. DefaultRunTimes];

    /// <summary>
    /// Windows timezone id preferred on Azure Functions Windows hosts.
    /// Also accepts IANA <c>Europe/London</c>; resolver tries both.
    /// </summary>
    [JsonPropertyName("timeZoneId")]
    [JsonPropertyOrder(20)]
    public string TimeZoneId { get; set; } = DefaultTimeZoneId;

    [JsonPropertyName("enabled")]
    [JsonPropertyOrder(30)]
    public bool Enabled { get; set; } = true;

    public override string FileKey => nameof(DiscoveryScheduleConfig);

    public static DiscoveryScheduleConfig CreateDefault() => new()
    {
        RunTimes = [.. DefaultRunTimes],
        TimeZoneId = DefaultTimeZoneId,
        Enabled = true
    };
}
