using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;

namespace MachineAuth0;

public class ApiClient(
    HttpClient httpClient,
    IAuth0Client auth0Client,
    IOptions<ApiOptions> apiOptions,
    ILogger<ApiClient> logger) : IApiClient
{
    private readonly ApiOptions _apiOptions = apiOptions.Value;

    public async Task Test()
    {
        var token = await auth0Client.GetClientToken();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await httpClient.GetAsync(new Uri(_apiOptions.Endpoint, "test"));
        if (response.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failure");
        }
        else
        {
            logger.LogInformation("Success");
        }
    }
}