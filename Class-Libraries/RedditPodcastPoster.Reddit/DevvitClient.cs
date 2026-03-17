using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;

namespace RedditPodcastPoster.Reddit;

public class DevvitClient(
    HttpClient httpClient,
    IAuth0Client auth0Client,
    IOptions<DevvitSettings> devvitSettings,
    ILogger<DevvitClient> logger) : IDevvitClient
{
    private readonly DevvitSettings _devvitSettings = devvitSettings.Value;

    public async Task<DevvitEpisodeCreateResponse> CreateEpisodePost(
        DevvitEpisodeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = await auth0Client.GetClientToken();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _devvitSettings.Endpoint)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Devvit create episode failed with status '{StatusCode}'. Response: '{ResponseContent}'.",
                response.StatusCode, content);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<DevvitEpisodeCreateResponse>(cancellationToken)
               ?? new DevvitEpisodeCreateResponse();
    }
}
