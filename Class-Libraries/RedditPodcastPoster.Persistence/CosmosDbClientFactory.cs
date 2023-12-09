using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbClientFactory : ICosmosDbClientFactory
{
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger<CosmosDbClientFactory> _logger;
    private readonly CosmosDbSettings _settings;

    public CosmosDbClientFactory(
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosDbClientFactory> logger)
    {
        _settings = settings.Value;
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider;
        _logger = logger;
    }

    public CosmosClient Create()
    {
        var cosmosClientOptions = new CosmosClientOptions
        {
            Serializer =
                new CosmosSystemTextJsonSerializer(_jsonSerializerOptionsProvider.GetJsonSerializerOptions())
        };
        if (_settings.UseGateway.HasValue && _settings.UseGateway.Value)
        {
            cosmosClientOptions.ConnectionMode = ConnectionMode.Gateway;
        }

        CosmosClient client = new(
            _settings.Endpoint,
            _settings.AuthKeyOrResourceToken,
            cosmosClientOptions
        );
        return client;
    }
}