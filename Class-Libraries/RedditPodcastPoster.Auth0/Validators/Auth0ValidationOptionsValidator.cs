using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0.Configuration;

namespace RedditPodcastPoster.Auth0.Validators;

public class Auth0ValidationOptionsValidator : IValidateOptions<Auth0ValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, Auth0ValidationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(Auth0ValidationOptions)}.{nameof(Auth0ValidationOptions.Audience)} must not be null or empty (config key: auth0__Audience).");
        }

        if (string.IsNullOrWhiteSpace(options.Domain))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(Auth0ValidationOptions)}.{nameof(Auth0ValidationOptions.Domain)} must not be null or empty (config key: auth0__Domain).");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(Auth0ValidationOptions)}.{nameof(Auth0ValidationOptions.Issuer)} must not be null or empty (config key: auth0__Issuer).");
        }

        return ValidateOptionsResult.Success;
    }
}
