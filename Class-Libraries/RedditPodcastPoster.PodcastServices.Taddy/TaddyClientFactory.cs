using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Taddy.Configuration;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public class TaddyClientFactory(
    IOptions<TaddyOptions> taddyOptions,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<TaddyClientFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ITaddyClientFactory
{
    private readonly TaddyOptions _taddyOptions = taddyOptions.Value;

    public GraphQLHttpClient Create()
    {
        var graphQLClient = new GraphQLHttpClient(
            "https://api.taddy.org/", new SystemTextJsonSerializer());
        graphQLClient.HttpClient.DefaultRequestHeaders.Add("X-API-KEY", _taddyOptions.ApiKey);
        graphQLClient.HttpClient.DefaultRequestHeaders.Add("X-USER-ID", _taddyOptions.Userid);
        return graphQLClient;
    }
}