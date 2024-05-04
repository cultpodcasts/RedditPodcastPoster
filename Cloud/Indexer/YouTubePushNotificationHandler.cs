using System.Net;
using System.Web;
using System.Xml.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.YouTubePushNotifications;

namespace Indexer;

public class YouTubePushNotificationHandler(
    IPodcastsSubscriber podcastsSubscriber,
    IPushNotificationHandler pushNotificationHandler,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<YouTubePushNotificationHandler>();

//    [Function("YouTubeSubscriptionChallenge")]
    public async Task<HttpResponseData> YouTubeSubscriptionChallenge(
//        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "youtubenotification/{podcastId}")]
        HttpRequestData req,
        Guid podcastId)
    {
        const string hubChallenge = "hub.challenge";
        const string hubLeaseSeconds = "hub.lease_seconds";
        const string mode = "hub.mode";

        _logger.LogInformation(
            $"{nameof(YouTubeSubscriptionChallenge)} - Podcast-Id: '{podcastId}', url: '{req.Url}'.");
        var queryString = HttpUtility.ParseQueryString(req.Url.Query);

        try
        {
            if (queryString.AllKeys.Contains(hubLeaseSeconds))
            {
                var leaseSecondsParam = queryString[hubLeaseSeconds];
                if (long.TryParse(leaseSecondsParam, out var leaseSeconds))
                {
                    await podcastsSubscriber.UpdateLease(podcastId, leaseSeconds);
                }
                else
                {
                    _logger.LogError(
                        $"Unable to parse Long from url-param '{nameof(hubLeaseSeconds)}' from url '{req.Url}' with value '{leaseSecondsParam}'.");
                }
            }
            else
            {
                if (queryString.AllKeys.Contains(mode) && queryString[mode] == Constants.ModeUnsubscribe)
                {
                    await podcastsSubscriber.RemoveLease(podcastId);
                }
                else
                {
                    _logger.LogError(
                        $"Missing url-param '{nameof(mode)}' for presumed unsubscribe-message from url '{req.Url}'.");
                }
            }

            if (queryString.AllKeys.Contains(hubChallenge))
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

                await response.WriteStringAsync(queryString[hubChallenge]!);
                return response;
            }

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to execute {nameof(YouTubeSubscriptionChallenge)}");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

 //   [Function("YouTubePushNotification")]
    public async Task<HttpResponseData> YouTubePushNotification(
 //       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "youtubenotification/{podcastId}")]
        HttpRequestData req,
        Guid podcastId,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(
                $"{nameof(YouTubePushNotificationHandler)} - Podcast-Id: '{podcastId}', url: '{req.Url}'.");
            var xml = await XDocument.LoadAsync(req.Body, LoadOptions.None, ct);
            await pushNotificationHandler.Handle(podcastId, xml);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to execute {nameof(YouTubePushNotification)}");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

//    [Function("YouTubeSubscriber")]
    public async Task YouTubeSubscriber(
//        [TimerTrigger("0 12 * * *"
//#if DEBUG
//            , RunOnStartup = true
//#endif
 //       )]
        TimerInfo info)
    {
        try
        {
            _logger.LogInformation($"{nameof(YouTubePushNotificationHandler)} {nameof(YouTubeSubscriber)} initiated.");
            await podcastsSubscriber.SubscribePodcasts();
            _logger.LogInformation($"{nameof(YouTubePushNotificationHandler)} {nameof(YouTubeSubscriber)} complete.");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failed to execute {nameof(YouTubeSubscriber)}");
        }
    }
}