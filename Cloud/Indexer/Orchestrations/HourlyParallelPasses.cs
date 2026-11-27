using RedditPodcastPoster.Configuration.Options;

namespace Indexer.Orchestrations;

/// <summary>
/// Pure decision for whether the hour's two Indexer activities run side by side.
/// YouTube-enabled hours (<c>hourUtc % 3 == 0</c>) stay sequential: the quota tracker is a
/// singleton per Flex instance (same-instance accumulate is safe under its lock), but Durable
/// may dispatch the two activities to different workers — each would hydrate the same Cosmos
/// baseline and last-write-wins on absolute <c>FlushToCosmosAsync</c>.
/// </summary>
public static class HourlyParallelPasses
{
    public const string YouTubeHourReason = "youtube-hour";

    public static (bool Parallel, string Reason) Decide(ParallelPassesMode mode, int hourUtc)
    {
        var modeWantsParallel = mode switch
        {
            ParallelPassesMode.All => true,
            ParallelPassesMode.OddHours => hourUtc % 2 == 1,
            _ => false
        };

        if (!modeWantsParallel)
        {
            return (false, string.Empty);
        }

        if (hourUtc % 3 == 0)
        {
            return (false, YouTubeHourReason);
        }

        return (true, string.Empty);
    }
}
