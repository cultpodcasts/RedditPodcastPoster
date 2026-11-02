using Microsoft.Extensions.Options;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Validators;

/// <summary>
/// Validates <see cref="TwitterOptions"/> for posting hosts (Indexer, Api, Poster).
/// Not registered by <c>AddXOAuth2TokenServices</c> — the exchange/authorize consoles report missing
/// credentials themselves.
/// </summary>
public class TwitterOptionsValidator : IValidateOptions<TwitterOptions>
{
    public ValidateOptionsResult Validate(string? name, TwitterOptions options)
    {
        var failures = new List<string>();

        if (options.RequestTimeoutSeconds <= 0)
        {
            failures.Add(
                $"{nameof(TwitterOptions)}.{nameof(TwitterOptions.RequestTimeoutSeconds)} must be > 0 " +
                $"(config key: {TwitterOptions.SectionName}__{nameof(TwitterOptions.RequestTimeoutSeconds)}).");
        }

        if (options.ApiBaseAddress is not { IsAbsoluteUri: true })
        {
            failures.Add(
                $"{nameof(TwitterOptions)}.{nameof(TwitterOptions.ApiBaseAddress)} must be an absolute URI " +
                $"(config key: {TwitterOptions.SectionName}__{nameof(TwitterOptions.ApiBaseAddress)}).");
        }

        switch (options.AuthMode)
        {
            case TwitterAuthMode.OAuth1:
                Require(failures, options.ConsumerKey, nameof(TwitterOptions.ConsumerKey));
                Require(failures, options.ConsumerSecret, nameof(TwitterOptions.ConsumerSecret));
                Require(failures, options.AccessToken, nameof(TwitterOptions.AccessToken));
                Require(failures, options.AccessTokenSecret, nameof(TwitterOptions.AccessTokenSecret));
                break;
            case TwitterAuthMode.OAuth2:
                Require(failures, options.OAuth2ClientId, nameof(TwitterOptions.OAuth2ClientId));
                Require(failures, options.OAuth2ClientSecret, nameof(TwitterOptions.OAuth2ClientSecret));
                break;
            default:
                failures.Add(
                    $"{nameof(TwitterOptions)}.{nameof(TwitterOptions.AuthMode)} must be " +
                    $"{nameof(TwitterAuthMode.OAuth1)} or {nameof(TwitterAuthMode.OAuth2)} " +
                    $"(config key: {TwitterOptions.SectionName}__{nameof(TwitterOptions.AuthMode)}).");
                break;
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(List<string> failures, string? value, string property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add(
                $"{nameof(TwitterOptions)}.{property} must not be null or empty " +
                $"(config key: {TwitterOptions.SectionName}__{property}).");
        }
    }
}
