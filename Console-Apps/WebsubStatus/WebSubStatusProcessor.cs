using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.YouTubePushNotifications;
using System.Web;
using RedditPodcastPoster.Persistence.Abstractions;

namespace WebsubStatus;

public class WebSubStatusProcessor(
    IPodcastRepository podcastRepository,
    IPodcastYouTubePushNotificationUrlAdaptor podcastYouTubePushNotificationUrlAdaptor,
    HttpClient httpClient,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<WebSubStatusProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private const string WebSubEndpoint = "https://pubsubhubbub.appspot.com/subscription-details";

    public async Task Process(WebSubStatusRequest request)
    {
        var podcastIds = await podcastRepository.GetAllIds().ToArrayAsync();
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast == null)
            {
                throw new InvalidOperationException($"Podcast with id '{podcastId}' not found.");
            }

            if (podcast.IndexAllEpisodes && !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
            {
                if (podcast.YouTubeNotificationSubscriptionLeaseExpiry < DateTime.UtcNow)
                {
                    logger.LogError("Podcast '{PodcastName}' has expired subscription-lease. Id='{PodcastId}'.", podcast.Name, podcast.Id);
                }

                var (callbackUrl, topicUrl) =
                    podcastYouTubePushNotificationUrlAdaptor.GetPodcastSubscriptionUrls(podcast);

                var queryString = HttpUtility.ParseQueryString("");
                queryString.Add("hub.callback", callbackUrl.ToString());
                queryString.Add("hub.topic", topicUrl.ToString());

                var url = new Uri($"{WebSubEndpoint}?{queryString}");
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var document = new HtmlDocument();
                document.Load(await response.Content.ReadAsStreamAsync());
                var container =
                    document.DocumentNode
                        .SelectNodes("//dl[contains(@class, \"glue-body\")]").FirstOrDefault();
                if (container == null)
                {
                    throw new InvalidOperationException("Unable to locate dl html-element with class glue-body");
                }

                var values = container.SelectNodes("//dd");
                var status = values[1].InnerText;
                if (status == "verified")
                {
                    logger.LogInformation("Podcast '{PodcastName}' is verified.", podcast.Name);
                }
                else
                {
                    logger.LogError("Podcast '{PodcastName}' is '{Status}'.", podcast.Name, status);
                }
            }
        }
    }
}