using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.CloudflareRedirect.Dtos;
using RedditPodcastPoster.Configuration;

namespace RedditPodcastPoster.CloudflareRedirect;

public class RedirectService(
    HttpClient httpClient,
    IOptions<CloudFlareOptions> cloudFlareOptions,
    IOptions<RedirectOptions> redirectOptions,
    ILogger<RedirectService> logger) : IRedirectService
{
    private readonly CloudFlareOptions _cloudFlareOptions = cloudFlareOptions.Value;
    private readonly string _podcastBasePath = redirectOptions.Value.PodcastBasePath.ToString();
    private readonly RedirectOptions _redirectOptions = redirectOptions.Value;

    public async Task<bool> CreatePodcastRedirect(PodcastRedirect podcastRedirect)
    {
        if (await IsSafe(podcastRedirect))
        {
            var url = GetItemsUrl();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _cloudFlareOptions.ListsApiToken);
            var target = new Uri(_redirectOptions.PodcastBasePath, podcastRedirect.NewPodcastName.Replace("?", "%3F"));
            var getListItemsResult = new GetListItemsResult
            {
                Comment = $"Redirect '{podcastRedirect.OldPodcastName}' -> '{podcastRedirect.NewPodcastName}'.",
                Redirect = new GetListItemsResultRedirect
                {
                    PreservePathSuffix = true,
                    PreserveQueryString = true,
                    SubpathMatching = true,
                    StatusCode = (int) HttpStatusCode.PermanentRedirect,
                    SourceUrl =
                        new Uri(_redirectOptions.PodcastBasePath, podcastRedirect.OldPodcastName).ToString()
                            .Substring(_redirectOptions.PodcastBasePath.Scheme.Length + 3),
                    TargetUrl = target.ToString()
                }
            };
            var redirect =
                requestMessage.Content = JsonContent.Create(new[] {getListItemsResult},
                    options: new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
            var httpResponse = await httpClient.SendAsync(requestMessage);
            if (httpResponse.IsSuccessStatusCode)
            {
                return true;
            }

            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            logger.LogError($"Unable to create podcast-redirect. Response: {responseContent}");
        }
        else
        {
            logger.LogError(
                $"Unable to create podcast-redirect. Redirect considered unsafe. '{podcastRedirect.OldPodcastName}' -> '{podcastRedirect.NewPodcastName}'.");
        }
        return false;
    }

    private async Task<bool> IsSafe(PodcastRedirect podcastRedirect)
    {
        var allRedirects = await GetAllPodcastRedirects();
        var newLower = podcastRedirect.NewPodcastName.ToLowerInvariant();
        var oldLower = podcastRedirect.OldPodcastName.ToLowerInvariant();

        var chainElements = allRedirects.Where(x =>
            x.NewPodcastName.ToLowerInvariant() == newLower ||
            x.NewPodcastName.ToLowerInvariant() == oldLower ||
            x.OldPodcastName.ToLowerInvariant() == newLower ||
            x.OldPodcastName.ToLowerInvariant() == oldLower);

        if (chainElements.Any(x => x.OldPodcastName.ToLowerInvariant() == newLower))
        {
            return false;
        }

        return true;
    }

    private async Task<List<PodcastRedirect>> GetAllPodcastRedirects()
    {
        GetListItemsResponse? response = null;
        var redirects = new List<PodcastRedirect>();
        do
        {
            var url = GetItemsUrl(response?.Info.Cursors.After);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _cloudFlareOptions.ListsApiToken);
            var httpResponse = await httpClient.SendAsync(requestMessage);
            if (httpResponse.IsSuccessStatusCode)
            {
                try
                {
                    response = await httpResponse.Content.ReadFromJsonAsync<GetListItemsResponse>();
                    var podcastPathRedirects = response.Results
                        .Where(x =>
                            x.Redirect.SourceUrl.ToLowerInvariant().StartsWith(_podcastBasePath.ToLowerInvariant()
                                .Substring(_redirectOptions.PodcastBasePath.Scheme.Length + 3)) &&
                            x.Redirect.TargetUrl.ToLowerInvariant().StartsWith(_podcastBasePath.ToLowerInvariant())
                        );
                    var models = podcastPathRedirects
                        .Select(x =>
                            new PodcastRedirect(
                                x.Redirect.SourceUrl.Substring(_podcastBasePath
                                    .Substring(_redirectOptions.PodcastBasePath.Scheme.Length + 3).Length),
                                x.Redirect.TargetUrl.Substring(_podcastBasePath.Length)
                            ));
                    var filtered = models
                        .Where(x =>
                            !x.OldPodcastName.Contains("/") &&
                            !x.NewPodcastName.Contains("/"));
                    redirects.AddRange(filtered);
                }
                catch (Exception e) // Invalid JSON
                {
                    logger.LogError(e, "Failure calling cloudflare lists api.");
                    throw;
                }
            }
        } while (response != null && !string.IsNullOrWhiteSpace(response.Info?.Cursors.After));

        return redirects;
    }

    private Uri GetItemsUrl(string? cursor = null)
    {
        var url = new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{_cloudFlareOptions.AccountId}/rules/lists/{_redirectOptions.PodcastRedirectRulesId}/items");
        var urlBuilder = new UriBuilder(url);
        if (cursor != null)
        {
            var query = HttpUtility.ParseQueryString(urlBuilder.Query);
            query["cursor"] = cursor;
            urlBuilder.Query = query.ToString();
        }

        return urlBuilder.Uri;
    }
}