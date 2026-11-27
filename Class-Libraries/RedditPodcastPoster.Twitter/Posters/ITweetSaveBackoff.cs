namespace RedditPodcastPoster.Twitter.Posters;

/// <summary>
/// The wait between attempts to save a document as tweeted after X accepted the post (#1205). A seam so tests
/// do not sleep; production uses <see cref="ExponentialTweetSaveBackoff"/>.
/// </summary>
public interface ITweetSaveBackoff
{
    /// <param name="failedAttempt">1-based number of the save attempt that just failed.</param>
    Task Delay(int failedAttempt, CancellationToken cancellationToken = default);
}

public sealed class ExponentialTweetSaveBackoff : ITweetSaveBackoff
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);

    public Task Delay(int failedAttempt, CancellationToken cancellationToken = default) =>
        Task.Delay(BaseDelay * Math.Pow(2, Math.Max(0, failedAttempt - 1)), cancellationToken);
}
