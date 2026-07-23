using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Configuration;

namespace RedditPodcastPoster.Persistence.Validators;

public class CosmosDbSettingsValidator : IValidateOptions<CosmosDbSettings>
{
    public ValidateOptionsResult Validate(string? name, CosmosDbSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.Endpoint)} must not be null or empty (config key: cosmosdb__Endpoint).");
        }

        if (string.IsNullOrWhiteSpace(options.AuthKeyOrResourceToken))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.AuthKeyOrResourceToken)} must not be null or empty (config key: cosmosdb__AuthKeyOrResourceToken).");
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseId))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.DatabaseId)} must not be null or empty (config key: cosmosdb__DatabaseId).");
        }

        if (string.IsNullOrWhiteSpace(options.PodcastsContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.PodcastsContainer)} must not be null or empty (config key: cosmosdb__PodcastsContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.EpisodesContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.EpisodesContainer)} must not be null or empty (config key: cosmosdb__EpisodesContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.SubjectsContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.SubjectsContainer)} must not be null or empty (config key: cosmosdb__SubjectsContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.PeopleContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.PeopleContainer)} must not be null or empty (config key: cosmosdb__PeopleContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.ActivitiesContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.ActivitiesContainer)} must not be null or empty (config key: cosmosdb__ActivitiesContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.DiscoveryContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.DiscoveryContainer)} must not be null or empty (config key: cosmosdb__DiscoveryContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.LookUpsContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.LookUpsContainer)} must not be null or empty (config key: cosmosdb__LookUpsContainer).");
        }

        if (string.IsNullOrWhiteSpace(options.PushSubscriptionsContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(CosmosDbSettings)}.{nameof(CosmosDbSettings.PushSubscriptionsContainer)} must not be null or empty (config key: cosmosdb__PushSubscriptionsContainer).");
        }

        return ValidateOptionsResult.Success;
    }
}
