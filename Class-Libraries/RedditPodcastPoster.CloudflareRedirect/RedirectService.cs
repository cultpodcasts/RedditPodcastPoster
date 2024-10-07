using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    private readonly RedirectOptions _redirectOptions = redirectOptions.Value;

    public Task<CreateRedirectResult> CreatePodcastRedirect(PodcastRedirect podcastRedirect)
    {
        throw new NotImplementedException();
    }

    public async Task<List<PodcastRedirect>> GetPodcastRedirectChain(PodcastRedirect podcastRedirect)
    {
        throw new NotImplementedException();
    }

    public async Task<List<PodcastRedirect>> GetAllPodcastRedirects()
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
                    redirects.AddRange(response.Results.Select(x =>
                        new PodcastRedirect(x.Redirect.SourceUrl, x.Redirect.TargetUrl)));
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

    private Uri GetItemsUrl(string? cursor)
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