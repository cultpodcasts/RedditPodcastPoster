using System.Text.Json.Serialization;

using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Models.YouTubeQuota;

[CosmosSelector(ModelType.YouTubeQuotaReport)]
public sealed class YouTubeQuotaReport : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("6d1f4b8a-3c2e-4d7f-9a5b-8e0c1f2a3b4d");

    public const int DaysRetained = 7;

    public YouTubeQuotaReport()
    {
        Id = _Id;
        ModelType = ModelType.YouTubeQuotaReport;
    }

    [JsonPropertyName("sourceApplication")]
    [JsonPropertyOrder(10)]
    public required string SourceApplication { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(11)]
    public DateTime UpdatedUtc { get; set; }

    [JsonPropertyName("days")]
    [JsonPropertyOrder(12)]
    public List<YouTubeQuotaDailyReport> Days { get; set; } = [];

    /// <summary>
    ///     Replaces any existing entry for the day's report-date, then prunes entries older than
    ///     <see cref="DaysRetained" /> days relative to the newest report-date, keeping days ordered newest-first.
    /// </summary>
    public void UpsertDay(YouTubeQuotaDailyReport day)
    {
        Days.RemoveAll(x => x.ReportDate == day.ReportDate);
        Days.Add(day);

        var cutoff = Days.Max(x => x.ReportDate).AddDays(-(DaysRetained - 1));
        Days.RemoveAll(x => x.ReportDate < cutoff);
        Days.Sort((a, b) => b.ReportDate.CompareTo(a.ReportDate));
    }

    public override string FileKey => nameof(YouTubeQuotaReport);
}
