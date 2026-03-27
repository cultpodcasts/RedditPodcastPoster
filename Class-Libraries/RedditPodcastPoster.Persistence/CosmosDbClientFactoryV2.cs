using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbClientFactoryV2(
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<CosmosDbSettings> settings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbClientFactoryV2> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ICosmosDbClientFactoryV2
{
    private readonly CosmosDbSettings _settings = settings.Value;

    public CosmosClient Create()
    {
        var cosmosClientOptions = new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = jsonSerializerOptionsProvider.GetJsonSerializerOptions()
        };

        if (_settings.UseGateway.HasValue && _settings.UseGateway.Value)
        {
            cosmosClientOptions.ConnectionMode = ConnectionMode.Gateway;
        }

        CosmosClient client = new(_settings.Endpoint, _settings.AuthKeyOrResourceToken, cosmosClientOptions);
        return client;
    }
}
