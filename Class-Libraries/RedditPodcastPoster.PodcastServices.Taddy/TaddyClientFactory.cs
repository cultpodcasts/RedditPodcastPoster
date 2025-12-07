using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Taddy.Configuration;

namespace RedditPodcastPoster.PodcastServices.Taddy;

public class TaddyClientFactory : ITaddyClientFactory
{
    private readonly TaddyOptions _taddyOptions;

    public TaddyClientFactory(IOptions<TaddyOptions> taddyOptions,
#pragma warning disable CS9113 // Parameter is unread.
        ILogger<TaddyClientFactory> logger)
    {
        _taddyOptions = taddyOptions.Value;
        if (_taddyOptions == null)
        {
            throw new ArgumentNullException($"Taddy-options ${nameof(IOptions<TaddyOptions>)}${nameof(taddyOptions.Value)} is null");
        }

        if (!string.IsNullOrWhiteSpace(_taddyOptions.ApiKey) && !string.IsNullOrWhiteSpace(_taddyOptions.Userid))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_taddyOptions.ApiKey) && string.IsNullOrWhiteSpace(_taddyOptions.Userid))
        {
            throw new ArgumentNullException($"Missing {nameof(TaddyOptions.ApiKey)} and {nameof(TaddyOptions.Userid)} in {nameof(TaddyOptions)}");
        }

        if (string.IsNullOrWhiteSpace(_taddyOptions.ApiKey))
        {
            throw new ArgumentNullException($"Missing {nameof(TaddyOptions.ApiKey)} in {nameof(TaddyOptions)}");
        }

        throw new ArgumentNullException($"Missing {nameof(TaddyOptions.Userid)} in {nameof(TaddyOptions)}");

    }

    public GraphQLHttpClient Create()
    {
        var graphQLClient = new GraphQLHttpClient(
            "https://api.taddy.org/", new SystemTextJsonSerializer());
        graphQLClient.HttpClient.DefaultRequestHeaders.Add("X-API-KEY", _taddyOptions.ApiKey);
        graphQLClient.HttpClient.DefaultRequestHeaders.Add("X-USER-ID", _taddyOptions.Userid);
        return graphQLClient;
    }
}