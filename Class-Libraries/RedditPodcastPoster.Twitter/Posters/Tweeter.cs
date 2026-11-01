using Microsoft.Extensions.Logging;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Twitter.Posters;

public class Tweeter(
    ITweetPoster tweetPoster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
    ICataloguePlayableSocialPosts cataloguePlayableSocialPosts,
    ILogger<Tweeter> logger)
    : ITweeter
{
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

        var tweeted = false;
        var stopRun = false;
        foreach (var podcastEpisode in untweeted.ToArray())
        {
            if (tweeted || stopRun)
            {
                break;
            }

            (tweeted, stopRun) = await TryTweet(podcastEpisode);
        }

        if (tweeted || stopRun)
        {
            return;
        }

        // Podcast episodes go first. A TV episode, news report or film is tweeted only when no podcast
        // episode was, so the one-tweet-per-run cadence is unchanged.
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
            if (tweeted || stopRun)
            {
                break;
            }

            (tweeted, stopRun) = await TryTweet(item);
        }
    }

    /// <summary>
    /// Sends one candidate. <c>StopRun</c> is true when X refused for a reason that applies to every candidate
    /// (rate limit, depleted credits, rejected credentials), so the run ends without trying, and paying for, the rest.
    /// The candidate keeps <c>Tweeted=false</c> and is tried again on the next run.
    /// </summary>
    private async Task<(bool Tweeted, bool StopRun)> TryTweet(PodcastEpisode podcastEpisode)
    {
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
            var stopRun = StopsRun(tweetStatus.TweetSendStatus);
            if (stopRun)
            {
                LogRunStopped(tweetStatus.TweetSendStatus, podcastEpisode);
            }

            return (tweetStatus.TweetSendStatus == TweetSendStatus.Sent, stopRun);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unable to tweet {ContentKind} with id '{EpisodeId}' with title '{EpisodeTitle}' from parent with id '{PodcastId}' and name '{PodcastName}'.",
                podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id, podcastEpisode.Episode.Title,
                podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name);
            return (false, false);
        }
    }

    /// <summary>Statuses that mean no further post can succeed in this run.</summary>
    public static bool StopsRun(TweetSendStatus status) =>
        status is TweetSendStatus.TooManyRequests or TweetSendStatus.PaymentRequired
            or TweetSendStatus.AuthenticationFailed;

    private void LogRunStopped(TweetSendStatus status, PodcastEpisode podcastEpisode)
    {
        var reason = status switch
        {
            TweetSendStatus.PaymentRequired => "X credits depleted (402 Payment Required)",
            TweetSendStatus.AuthenticationFailed => "X rejected the credentials (401/403)",
            _ => "X rate limit reached (429 Too Many Requests)"
        };
        logger.LogWarning(
            "{Reason}; stopping tweet run. {ContentKind} with id '{EpisodeId}' stays untweeted and is retried next run.",
            reason, podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id);
    }
}
