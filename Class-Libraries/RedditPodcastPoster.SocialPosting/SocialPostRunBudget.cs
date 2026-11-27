using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.SocialPosting;

/// <summary>
/// Per-run post and failure budget shared by Twitter and Bluesky hourly posters.
/// Call <see cref="TryReserve"/> before each send attempt; then
/// <see cref="RecordSent"/>, <see cref="RecordSkip"/>, <see cref="RecordFailure"/>, or
/// <see cref="Stop"/> according to the platform's outcome mapping.
/// </summary>
public sealed class SocialPostRunBudget
{
    private readonly int _maxPosts;
    private readonly int _maxFailures;
    private readonly string _runLabel;
    private readonly ILogger _logger;

    public SocialPostRunBudget(int maxPosts, int maxFailures, string runLabel, ILogger logger)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPosts);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFailures);
        ArgumentException.ThrowIfNullOrWhiteSpace(runLabel);
        ArgumentNullException.ThrowIfNull(logger);

        _maxPosts = maxPosts;
        _maxFailures = maxFailures;
        _runLabel = runLabel;
        _logger = logger;
    }

    public int Posts { get; private set; }

    public int Failures { get; private set; }

    public bool Stopped { get; private set; }

    /// <summary>
    /// True when the run may still attempt another send (not stopped and under the post cap).
    /// </summary>
    public bool HasCapacity => !Stopped && Posts < _maxPosts;

    /// <summary>
    /// Reserves permission to attempt one send. Returns false when the run is already stopped or
    /// the post cap is reached (marks <see cref="Stopped"/> and logs Information).
    /// Does not increment <see cref="Posts"/> — call <see cref="RecordSent"/> only after a real send.
    /// </summary>
    public bool TryReserve()
    {
        if (Stopped)
        {
            return false;
        }

        if (Posts >= _maxPosts)
        {
            _logger.LogInformation(
                "Stopping {RunLabel} because max-posts limit was reached. Posts: {Posts}, Max-posts: {MaxPosts}.",
                _runLabel,
                Posts,
                _maxPosts);
            Stopped = true;
            return false;
        }

        return true;
    }

    /// <summary>A successful send. Counts against the post cap.</summary>
    public void RecordSent() => Posts++;

    /// <summary>
    /// A resolved skip that spends neither the post nor the failure budget (e.g. Twitter
    /// Untweetable / DuplicateForbidden). Counters are unchanged.
    /// </summary>
    public void RecordSkip()
    {
        // Intentionally empty: skips continue the run without spending Posts or Failures.
    }

    /// <summary>
    /// Counts an unsuccessful send toward the failure cap. Returns false when the cap is reached
    /// (marks <see cref="Stopped"/> and logs Warning).
    /// </summary>
    public bool RecordFailure()
    {
        Failures++;
        if (Failures >= _maxFailures)
        {
            _logger.LogWarning(
                "Stopping {RunLabel} because failure threshold was reached. Failures: {Failures}, Max-failures: {MaxFailures}.",
                _runLabel,
                Failures,
                _maxFailures);
            Stopped = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ends the run without counting a post or a failure (e.g. rate limit, credits depleted,
    /// or a Bluesky non-retriable HTTP status).
    /// </summary>
    public void Stop() => Stopped = true;
}
