using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.ML.Services;

public static class DiscoveryFeatureBuilder
{
    public const int EmbeddingDimensions = 384;

    public static string BuildEmbeddingText(DiscoveryResult result)
    {
        var show = result.ShowName?.Trim() ?? string.Empty;
        var episode = result.EpisodeName?.Trim() ?? string.Empty;
        var description = result.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            description = result.ShowDescription?.Trim() ?? string.Empty;
        }

        return $"{show}\n{episode}\n{description}";
    }

    public static string BuildEmbeddingText(string? showName, string? episodeName, string? description, string? showDescription)
    {
        var show = showName?.Trim() ?? string.Empty;
        var episode = episodeName?.Trim() ?? string.Empty;
        var text = description?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            text = showDescription?.Trim() ?? string.Empty;
        }

        return $"{show}\n{episode}\n{text}";
    }

    public static DiscoveryNumericFeatures BuildNumericFeatures(
        DiscoveryResult result,
        IReadOnlyDictionary<string, float>? showAcceptRates)
    {
        return BuildNumericFeatures(
            result.MatchingPodcastIds.Length,
            result.Subjects,
            result.Sources,
            result.ShowName,
            showAcceptRates);
    }

    public static DiscoveryNumericFeatures BuildNumericFeatures(
        int matchingPodcastCount,
        IEnumerable<string>? subjects,
        DiscoverService[] sources,
        string? showName,
        IReadOnlyDictionary<string, float>? showAcceptRates)
    {
        var subjectCount = subjects?.Count() ?? 0;
        var showRate = 0f;
        if (!string.IsNullOrWhiteSpace(showName) &&
            showAcceptRates != null &&
            showAcceptRates.TryGetValue(showName.Trim(), out var rate))
        {
            showRate = rate;
        }

        return new DiscoveryNumericFeatures
        {
            HasMatchingPodcast = matchingPodcastCount > 0 ? 1f : 0f,
            SubjectCount = Math.Min(subjectCount, 20) / 20f,
            SourceListenNotes = sources.Contains(DiscoverService.ListenNotes) ? 1f : 0f,
            SourceSpotify = sources.Contains(DiscoverService.Spotify) ? 1f : 0f,
            SourceYouTube = sources.Contains(DiscoverService.YouTube) ? 1f : 0f,
            SourceTaddy = sources.Contains(DiscoverService.Taddy) ? 1f : 0f,
            ShowAcceptRate = showRate
        };
    }

    public static DiscoveryNumericFeatures BuildNumericFeatures(
        Guid[] matchingPodcastIds,
        IEnumerable<string>? subjects,
        DiscoverService[] sources,
        string? showName,
        IReadOnlyDictionary<string, float>? showAcceptRates)
    {
        return BuildNumericFeatures(
            matchingPodcastIds.Length,
            subjects,
            sources,
            showName,
            showAcceptRates);
    }
}

public sealed class DiscoveryNumericFeatures
{
    public float HasMatchingPodcast { get; init; }
    public float SubjectCount { get; init; }
    public float SourceListenNotes { get; init; }
    public float SourceSpotify { get; init; }
    public float SourceYouTube { get; init; }
    public float SourceTaddy { get; init; }
    public float ShowAcceptRate { get; init; }

    public float[] ToArray() =>
    [
        HasMatchingPodcast,
        SubjectCount,
        SourceListenNotes,
        SourceSpotify,
        SourceYouTube,
        SourceTaddy,
        ShowAcceptRate
    ];
}
