using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Serialization;

namespace Api.Dtos;

public class DiscoveryScheduleResponse
{
    [JsonPropertyName("runTimes")]
    public required IReadOnlyList<string> RunTimes { get; init; }

    [JsonPropertyName("timeZoneId")]
    public required string TimeZoneId { get; init; }

    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    [JsonPropertyName("isDefault")]
    public required bool IsDefault { get; init; }

    [JsonPropertyName("nextRuns")]
    public required IReadOnlyList<NextRun> NextRuns { get; init; }

    public class NextRun
    {
        [JsonPropertyName("slotId")]
        public required string SlotId { get; init; }

        /// <summary>The slot start as a UTC instant, written with a trailing <c>Z</c> like every other API date-time.</summary>
        [JsonPropertyName("slotStartUtc")]
        public required DateTimeOffset SlotStartUtc { get; init; }

        /// <summary>
        /// EXCEPTION to "the API always emits UTC": the same slot start as UK local wall-clock time with its UK offset
        /// (<c>+01:00</c> in BST, <c>+00:00</c> in GMT). Curators set discovery run times in their local time, and the
        /// UTC instant of a run time moves when the clocks change, so this field keeps the local offset instead of being
        /// flattened to <c>Z</c> by the API's UTC date-time converters.
        /// </summary>
        [JsonPropertyName("slotStartUk")]
        [JsonConverter(typeof(LocalOffsetDateTimeOffsetConverter))]
        public required DateTimeOffset SlotStartUk { get; init; }
    }
}
