using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        var run = new TweetRun();
        foreach (var podcastEpisode in untweeted.ToArray())
        {
            if (!await TryTweet(podcastEpisode, run))
            {
                break;
            }
        }

        // Podcast episodes go first. TV episodes, news reports and films fill any remaining
        // slots in the same max-posts and failure budget.
        if (!run.Stopped && run.Posts < _options.MaxPosts)
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

    private sealed class TweetRun
    {
        public int Posts { get; set; }
        public int Failures { get; set; }
        public bool Stopped { get; set; }
    }

    /// <summary>
    /// Sends one candidate. Returns false when the run must stop: the post cap was reached, the
    /// failure cap was reached, or X refused for a reason that applies to every candidate
    /// (rate limit, depleted credits, rejected credentials). A run-wide refusal leaves the
    /// candidate with <c>Tweeted=false</c> so it is tried again on the next run.
    /// </summary>
    private async Task<bool> TryTweet(PodcastEpisode podcastEpisode, TweetRun run)
    {
        if (run.Posts >= _options.MaxPosts)
        {
            logger.LogInformation(
                "Stopping tweet run because max-posts limit was reached. Posts: {Posts}, Max-posts: {MaxPosts}.",
                run.Posts,
                _options.MaxPosts);
            run.Stopped = true;
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
            if (StopsRun(tweetStatus.TweetSendStatus))
            {
                LogRunStopped(tweetStatus.TweetSendStatus, podcastEpisode);
                run.Stopped = true;
                return false;
            }

            if (tweetStatus.TweetSendStatus == TweetSendStatus.Sent)
            {
                run.Posts++;
                return true;
            }

            if (IsResolvedSkip(tweetStatus.TweetSendStatus))
            {
                logger.LogInformation(
                    "Skipping {ContentKind} with id '{EpisodeId}' after {Status}; it is already resolved and does not count against the post or failure budget.",
                    podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id,
                    tweetStatus.TweetSendStatus);
                return true;
            }

            return RecordFailure(run);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unable to tweet {ContentKind} with id '{EpisodeId}' with title '{EpisodeTitle}' from parent with id '{PodcastId}' and name '{PodcastName}'.",
                podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id, podcastEpisode.Episode.Title,
                podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name);
            return RecordFailure(run);
        }
    }

    private bool RecordFailure(TweetRun run)
    {
        run.Failures++;
        if (run.Failures >= _options.MaxFailures)
        {
            logger.LogWarning(
                "Stopping tweet run because failure threshold was reached. Failures: {Failures}, Max-failures: {MaxFailures}.",
                run.Failures,
                _options.MaxFailures);
            run.Stopped = true;
            return false;
        }

        return true;
    }

    /// <summary>Statuses that mean no further post can succeed in this run.</summary>
    public static bool StopsRun(TweetSendStatus status) =>
        status is TweetSendStatus.TooManyRequests or TweetSendStatus.PaymentRequired
            or TweetSendStatus.AuthenticationFailed;

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
            _ => "X rate limit reached (429 Too Many Requests)"
        };
        logger.LogWarning(
            "{Reason}; stopping tweet run. {ContentKind} with id '{EpisodeId}' stays untweeted and is retried next run.",
            reason, podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id);
    }
}
