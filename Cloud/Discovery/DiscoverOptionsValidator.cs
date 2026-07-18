using Microsoft.Extensions.Options;

namespace Discovery;

public class DiscoverOptionsValidator : IValidateOptions<DiscoverOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscoverOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SearchSince))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DiscoverOptions)}.{nameof(DiscoverOptions.SearchSince)} is required (config key: discover__SearchSince).");
        }

        if (options.LookbackMode is null)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DiscoverOptions)}.{nameof(DiscoverOptions.LookbackMode)} is required (config key: discover__LookbackMode). Set to '{nameof(DiscoveryLookbackMode.Static)}' or '{nameof(DiscoveryLookbackMode.Dynamic)}'.");
        }

        if (!Enum.IsDefined(options.LookbackMode.Value))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DiscoverOptions)}.{nameof(DiscoverOptions.LookbackMode)} value '{options.LookbackMode}' is invalid. Set to '{nameof(DiscoveryLookbackMode.Static)}' or '{nameof(DiscoveryLookbackMode.Dynamic)}'.");
        }

        return ValidateOptionsResult.Success;
    }
}
