using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Strategies;

internal static class IndexerKeyRingBuilder
{
    internal static IReadOnlyList<Application> GetFlatIndexerApplications(IEnumerable<Application> applications)
    {
        var seenApiKeys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Application>();
        foreach (var application in applications)
        {
            if (application.Usage != ApplicationUsage.Indexer)
            {
                continue;
            }

            if (!seenApiKeys.Add(application.ApiKey))
            {
                continue;
            }

            result.Add(application);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("No Indexer youtube-applications registered.");
        }

        return result;
    }

    internal static int GetHourFallbackRingIndex(int hour, int ringCount) =>
        ringCount == 0 ? 0 : hour % 4 * (ringCount / 4);

    internal static IReadOnlyList<ApplicationWrapper> Build(
        IEnumerable<Application> applications,
        int startRingIndex)
    {
        var flatApplications = GetFlatIndexerApplications(applications);
        if (startRingIndex < 0 || startRingIndex >= flatApplications.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startRingIndex),
                $"Indexer ring start index must be between 0 and {flatApplications.Count - 1}.");
        }

        var ring = new List<ApplicationWrapper>(flatApplications.Count);
        for (var offset = 0; offset < flatApplications.Count; offset++)
        {
            var canonicalIndex = (startRingIndex + offset) % flatApplications.Count;
            ring.Add(new ApplicationWrapper(flatApplications[canonicalIndex], canonicalIndex, 0));
        }

        return ring;
    }
}
