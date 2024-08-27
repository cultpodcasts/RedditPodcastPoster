using System.Text.Json.Nodes;
using HtmlAgilityPack;
using iTunesSearch.Library.Models;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class EnrichedApplePodcastResolver(
    IApplePodcastResolver applePodcastResolver,
    HttpClient httpClient,
    ILogger<EnrichedApplePodcastResolver> logger) : IEnrichedApplePodcastResolver
{
    public async Task<Podcast?> FindPodcast(FindApplePodcastRequest request)
    {
        var podcast = await applePodcastResolver.FindPodcast(request);
        if (podcast != null)
        {
            var applePodcastUrl = GetApplePodcastUrl(podcast.Id);
            HttpResponseMessage? applePodcastPage = null;
            try
            {
                applePodcastPage = await httpClient.GetAsync(applePodcastUrl);
                applePodcastPage.EnsureSuccessStatusCode();
            }
            catch (HttpIOException e)
            {
                logger.LogError(e,
                    $"Unable to retrieve apple-podcast-page at url '{applePodcastUrl}', http-request-error: '{e.HttpRequestError}'.");
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Unable to retrieve apple-podcast-page at url '{applePodcastUrl}'.");
            }

            if (applePodcastPage != null)
            {
                var document = new HtmlDocument();
                document.Load(await applePodcastPage.Content.ReadAsStreamAsync());
                var applePodcastDetailsNode =
                    document.DocumentNode.SelectSingleNode("//script[@id=\"schema:show\"]");
                if (applePodcastDetailsNode is {InnerText: not null})
                {
                    try
                    {
                        var podcastDetails = JsonNode.Parse(applePodcastDetailsNode.InnerText)!.AsObject();
                        var description = podcastDetails["description"]?.GetValue<string>();
                        podcast.Description = description;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unable to parse as JSON: 'content'.");
                    }
                }
                else
                {
                    logger.LogError(
                        $"Unable to locate <script id='schema:show'> tag with inner-text in url '{applePodcastUrl}'.");
                }
            }
        }

        return podcast;
    }

    private static Uri GetApplePodcastUrl(long id)
    {
        return new Uri($"https://podcasts.apple.com/podcast/id{id}");
    }
}