using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.YouTubePushNotifications;

namespace WebsubStatus;

public class WebSubStatusProcessor(
    IPodcastRepository podcastRepository,
    IPodcastYouTubePushNotificationUrlAdaptor podcastYouTubePushNotificationUrlAdaptor,
    HttpClient httpClient,
    ILogger<WebSubStatusProcessor> logger)
{
    private const string WebSubEndpoint = "https://pubsubhubbub.appspot.com/subscription-details";

    public async Task Process(WebSubStatusRequest request)
    {
        var podcastIds = await podcastRepository.GetAllIds();
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast.IndexAllEpisodes && !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
            {
                if (podcast.YouTubeNotificationSubscriptionLeaseExpiry < DateTime.UtcNow)
                {
                    logger.LogError($"Podcast '{podcast.Name}' has expired subscription-lease. Id='{podcast.Id}'.");
                }

                var (callbackUrl, topicUrl) =
                    podcastYouTubePushNotificationUrlAdaptor.GetPodcastSubscriptionUrls(podcast);

                var @params = new Dictionary<string, string>
                {
                    {"hub.callback", callbackUrl.ToString()},
                    {"hub.topic", topicUrl.ToString()}
                };

                var url = new Uri(QueryHelpers.AddQueryString(WebSubEndpoint, @params));
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var document = new HtmlDocument();
                document.Load(await response.Content.ReadAsStreamAsync());
                var container =
                    document.DocumentNode
                        .SelectNodes("//dl[contains(@class, \"glue-body\")]").FirstOrDefault();
                var values = container.SelectNodes("//dd");
                var status = values[1].InnerText;
                if (status == "verified")
                {
                    logger.LogInformation($"Podcast '{podcast.Name}' is verified.");
                }
                else
                {
                    logger.LogError($"Podcast '{podcast.Name}' is '{status}'.");
                }
            }
        }
    }
}