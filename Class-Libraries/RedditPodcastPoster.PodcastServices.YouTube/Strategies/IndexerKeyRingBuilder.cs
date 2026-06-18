using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Strategies;

internal static class IndexerKeyRingBuilder
{
    internal static IReadOnlyList<ApplicationWrapper> Build(
        IEnumerable<Application> applications,
        int startPrimaryIndex)
    {
        var indexerApplications = applications
            .Where(x => x.Usage == ApplicationUsage.Indexer)
            .ToArray();
        var primaries = indexerApplications.Where(x => x.Reattempt == null).ToArray();
        if (primaries.Length == 0)
        {
            throw new InvalidOperationException("No Indexer primary youtube-applications registered.");
        }

        if (startPrimaryIndex < 0 || startPrimaryIndex >= primaries.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startPrimaryIndex),
                $"Indexer primary index must be between 0 and {primaries.Length - 1}.");
        }

        var maxReattempt = indexerApplications.Max(x => x.Reattempt) ?? 0;
        var ring = new List<ApplicationWrapper>();
        var seenApiKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var slotOffset = 0; slotOffset < primaries.Length; slotOffset++)
        {
            var slotIndex = (startPrimaryIndex + slotOffset) % primaries.Length;
            for (var reattempt = 0; reattempt <= maxReattempt; reattempt++)
            {
                var application = ResolveApplication(indexerApplications, primaries, slotIndex, reattempt);
                if (application == null || !seenApiKeys.Add(application.ApiKey))
                {
                    continue;
                }

                ring.Add(new ApplicationWrapper(application, slotIndex, maxReattempt));
            }
        }

        return ring;
    }

    private static Application? ResolveApplication(
        Application[] indexerApplications,
        Application[] primaries,
        int slotIndex,
        int reattempt)
    {
        if (reattempt == 0)
        {
            return primaries[slotIndex];
        }

        return indexerApplications
            .Where(x => x.Reattempt == reattempt)
            .Skip(slotIndex)
            .FirstOrDefault();
    }
}
