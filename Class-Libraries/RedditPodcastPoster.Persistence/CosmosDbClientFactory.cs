﻿using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class CosmosDbClientFactory(
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<CosmosDbSettings> settings,
    ILogger<CosmosDbClientFactory> logger)
    : ICosmosDbClientFactory
{
    private readonly CosmosDbSettings _settings = settings.Value;

    public CosmosClient Create()
    {
        var cosmosClientOptions = new CosmosClientOptions
        {
            Serializer =
                new CosmosSystemTextJsonSerializer(jsonSerializerOptionsProvider.GetJsonSerializerOptions())
        };
        if (_settings.UseGateway.HasValue && _settings.UseGateway.Value)
        {
            cosmosClientOptions.ConnectionMode = ConnectionMode.Gateway;
        }

        logger.LogInformation(
            $"endpoint: '{_settings.Endpoint}', token: '{_settings.AuthKeyOrResourceToken.Substring(Math.Max(0, _settings.AuthKeyOrResourceToken.Length - 10))}'.");
        logger.LogInformation($"{nameof(IJsonSerializerOptionsProvider)}: '{jsonSerializerOptionsProvider}'.");

        CosmosClient client = new(
            _settings.Endpoint,
            _settings.AuthKeyOrResourceToken,
            cosmosClientOptions
        );
        return client;
    }
}