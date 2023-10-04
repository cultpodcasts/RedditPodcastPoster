using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.Common.Persistence;

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
        CosmosClient client = new(
            _settings.Endpoint,
            _settings.AuthKeyOrResourceToken,
            new CosmosClientOptions
            {
                Serializer =
                    new CosmosSystemTextJsonSerializer(_jsonSerializerOptionsProvider.GetJsonSerializerOptions())
            }
        );
        return client;
    }

    public static IServiceCollection AddCosmosClient(IServiceCollection services)
    {
        return services
            .AddScoped<ICosmosDbClientFactory, CosmosDbClientFactory>()
            .AddScoped(s => s.GetService<ICosmosDbClientFactory>()!.Create());
    }
}