using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.SocialPosting;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Twitter.Posters;

public class Tweeter(
    ITweetPoster tweetPoster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
    ICataloguePlayableSocialPosts cataloguePlayableSocialPosts,
    IOptions<TwitterOptions> options,
    ILogger<Tweeter> logger)
    : ITweeter
{
    private readonly TwitterOptions _options = options.Value;

    public async Task Tweet(
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null)
    {
        IEnumerable<PodcastEpisode> untweeted;
        try
        {
            untweeted = await podcastEpisodeProvider.GetUntweetedPodcastEpisodes(
                youTubeRefreshed,
                spotifyRefreshed,
                preloadedRecentCandidates);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        var run = new SocialPostRunBudget(_options.MaxPosts, _options.MaxFailures, "tweet run", logger);
        foreach (var podcastEpisode in untweeted.ToArray())
        {
            if (!await TryTweet(podcastEpisode, run))
            {
                break;
            }
        }

        // Podcast episodes go first. TV episodes, news reports and films fill any remaining
        // slots in the same max-posts and failure budget.
        if (run.HasCapacity)
        {
            IReadOnlyList<PodcastEpisode> catalogueUntweeted;
            try
            {
                catalogueUntweeted = await cataloguePlayableSocialPosts.GetUntweeted();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to find untweeted TV episodes, news reports or films.");
                return;
            }

            foreach (var item in catalogueUntweeted)
            {
                if (!await TryTweet(item, run))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Sends one candidate. Returns false when the run must stop: the post cap was reached, the
    /// failure cap was reached, or X refused for a reason that applies to every candidate
    /// (rate limit, depleted credits, rejected credentials). A run-wide refusal leaves the
    /// candidate with <c>Tweeted=false</c> so it is tried again on the next run.
    /// </summary>
    private async Task<bool> TryTweet(PodcastEpisode podcastEpisode, SocialPostRunBudget run)
    {
        if (!run.TryReserve())
        {
            return false;
        }

        try
        {
            Uri? shortUrl;
            var hasShareImage = false;
            if (podcastEpisode.IsCataloguePlayable)
            {
                // The KV short link is podcast-shaped ({podcastSafeName}/{episodeId}); link to the kind route instead.
                shortUrl = CataloguePlayableSiteLink.For(podcastEpisode);
            }
            else
            {
                var shortnerResult = await shortnerService.Write(podcastEpisode);
                if (!shortnerResult.Success)
                {
                    logger.LogError("Unsuccessful shortening-url.");
                }

                shortUrl = shortnerResult.Url;
                hasShareImage = shortnerResult.HasShareImage;
            }

            var tweetStatus = await tweetPoster.PostTweet(podcastEpisode, shortUrl, hasShareImage);
            // Single policy table: count live posts first, then stop when the status forbids continuing.
            if (CountsAsPosted(tweetStatus.TweetSendStatus))
            {
                run.RecordSent();
            }

            if (StopsRun(tweetStatus.TweetSendStatus))
            {
                LogRunStopped(tweetStatus.TweetSendStatus, podcastEpisode);
                run.Stop();
                return false;
            }

            if (tweetStatus.TweetSendStatus == TweetSendStatus.Sent)
            {
                return true;
            }

            if (IsResolvedSkip(tweetStatus.TweetSendStatus))
            {
                logger.LogInformation(
                    "Skipping {ContentKind} with id '{EpisodeId}' after {Status}; it is already resolved and does not count against the post or failure budget.",
                    podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id,
                    tweetStatus.TweetSendStatus);
                run.RecordSkip();
                return true;
            }

            return run.RecordFailure();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unable to tweet {ContentKind} with id '{EpisodeId}' with title '{EpisodeTitle}' from parent with id '{PodcastId}' and name '{PodcastName}'.",
                podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id, podcastEpisode.Episode.Title,
                podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name);
            return run.RecordFailure();
        }
    }

    /// <summary>Statuses that mean a post is (or may be) live on X and counts toward MaxPosts.</summary>
    public static bool CountsAsPosted(TweetSendStatus status) =>
        status is TweetSendStatus.Sent or TweetSendStatus.SentNotSaved;

    /// <summary>
    /// Statuses that mean no further post should be attempted in this run: account-wide refusals,
    /// uncertain timeouts, or a live post whose tweeted state could not be saved (#1205).
    /// </summary>
    public static bool StopsRun(TweetSendStatus status) =>
        status is TweetSendStatus.TooManyRequests or TweetSendStatus.PaymentRequired
            or TweetSendStatus.AuthenticationFailed or TweetSendStatus.ServerError or TweetSendStatus.Timeout
            or TweetSendStatus.SentNotSaved;

    /// <summary>
    /// The document is already persisted as done, so the candidate is a skip, not a failure
    /// and not a sent post. <see cref="TweetSendStatus.Untweetable"/> never reaches X.
    /// </summary>
    public static bool IsResolvedSkip(TweetSendStatus status) =>
        status is TweetSendStatus.Untweetable or TweetSendStatus.DuplicateForbidden;

    private void LogRunStopped(TweetSendStatus status, PodcastEpisode podcastEpisode)
    {
        var reason = status switch
        {
            TweetSendStatus.PaymentRequired => "X credits depleted (402 Payment Required or usage-capped)",
            TweetSendStatus.AuthenticationFailed => "X rejected the credentials (401/403)",
            TweetSendStatus.ServerError => "X answered with a server error (5xx)",
            TweetSendStatus.Timeout => "The request to X timed out (the post may have been accepted)",
            TweetSendStatus.SentNotSaved => "A post was accepted by X but could not be saved as tweeted",
            _ => "X rate limit reached (429 Too Many Requests)"
        };
        logger.LogWarning(
            "{Reason}; stopping tweet run. {ContentKind} with id '{EpisodeId}' is {Outcome}.",
            reason, podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id,
            status == TweetSendStatus.SentNotSaved
                ? "live on X but not recorded as tweeted; check it before the next run"
                : "left untweeted and retried next run");
    }
}
