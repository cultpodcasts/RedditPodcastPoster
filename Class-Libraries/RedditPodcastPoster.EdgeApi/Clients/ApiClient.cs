using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0.Clients;
using RedditPodcastPoster.EdgeApi.Configuration;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;

namespace RedditPodcastPoster.EdgeApi.Clients;

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
            logger.LogError("Edge API test endpoint failed with status {StatusCode}.", response.StatusCode);
        }
        else
        {
            logger.LogInformation("Edge API test endpoint succeeded.");
        }
    }

    public async Task AppendHeroEpisodes(IReadOnlyList<Guid> episodeIds, CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return;
        }

        var token = await auth0Client.GetClientToken();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_apiOptions.Endpoint, "hero-curation/episodes"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new AppendHeroEpisodesRequest
        {
            EpisodeIds = episodeIds.ToArray()
        });

        var episodeIdList = string.Join(',', episodeIds);
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (CloudflareChallengeResponse.LooksLikeBotChallenge(response.StatusCode, body))
            {
                var truncatedBody = CloudflareChallengeResponse.TruncateBody(body);
                var challengeException = CloudflareChallengeResponse.CreateException(response.StatusCode, body);
                logger.LogError(
                    challengeException,
                    "Hero auto-promote: AppendHeroEpisodes blocked by Cloudflare bot-mode challenge. Status={StatusCode}. EpisodeIds: {EpisodeIds}. Body: {Body}",
                    response.StatusCode,
                    episodeIdList,
                    truncatedBody);
                throw challengeException;
            }

            logger.LogError(
                "Hero auto-promote: AppendHeroEpisodes failed with status {StatusCode}. EpisodeIds: {EpisodeIds}. Body: {Body}",
                response.StatusCode,
                episodeIdList,
                body);
            response.EnsureSuccessStatusCode();
            return;
        }

        logger.LogInformation(
            "Hero auto-promote: AppendHeroEpisodes succeeded for {Count} episode(s). EpisodeIds: {EpisodeIds}.",
            episodeIds.Count,
            episodeIdList);
    }

    private sealed class AppendHeroEpisodesRequest
    {
        [JsonPropertyName("episodeIds")]
        public Guid[] EpisodeIds { get; init; } = [];
    }
}
