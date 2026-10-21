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
        var tooManyRequests = false;
        foreach (var podcastEpisode in untweeted.ToArray())
        {
            if (tweeted || tooManyRequests)
            {
                break;
            }

            (tweeted, tooManyRequests) = await TryTweet(podcastEpisode);
        }

        if (tweeted || tooManyRequests)
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
            if (tweeted || tooManyRequests)
            {
                break;
            }

            (tweeted, tooManyRequests) = await TryTweet(item);
        }
    }

    private async Task<(bool Tweeted, bool TooManyRequests)> TryTweet(PodcastEpisode podcastEpisode)
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
            return (tweetStatus.TweetSendStatus == TweetSendStatus.Sent,
                tweetStatus.TweetSendStatus == TweetSendStatus.TooManyRequests);
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
}
