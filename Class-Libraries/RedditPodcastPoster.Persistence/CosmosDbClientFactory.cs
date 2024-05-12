using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbClientFactory : ICosmosDbClientFactory
{
    private readonly CosmosDbSettings _settings;
    private readonly IJsonSerializerOptionsProvider _jsonSerializerOptionsProvider;
    private readonly ILogger<CosmosDbClientFactory> _logger;

    public CosmosDbClientFactory(
        IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosDbClientFactory> logger)
    {
        _jsonSerializerOptionsProvider = jsonSerializerOptionsProvider;
        _logger = logger;
        _settings = settings.Value;
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

        _logger.LogInformation($"endpoint: '{_settings.Endpoint}', token: '{_settings.AuthKeyOrResourceToken.Substring(Math.Max(0, _settings.AuthKeyOrResourceToken.Length - 10))}'.");
        _logger.LogInformation($"{nameof(IJsonSerializerOptionsProvider)}: '{_jsonSerializerOptionsProvider}'.");

        CosmosClient client = new(
            _settings.Endpoint,
            _settings.AuthKeyOrResourceToken,
            cosmosClientOptions
        );
        return client;
    }
}