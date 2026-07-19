using Microsoft.Extensions.Options;

namespace Discovery;

public class DiscoverOptionsValidator : IValidateOptions<DiscoverOptions>
{
    public ValidateOptionsResult Validate(string? name, DiscoverOptions options)
    {
        // Dynamic-only; no SearchSince / LookbackMode. Overlap is optional (defaults in calculator).
        if (options.DynamicLookbackOverlap is { } overlap && overlap < TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(DiscoverOptions)}.{nameof(DiscoverOptions.DynamicLookbackOverlap)} must be >= 00:00:00 (config key: discover__DynamicLookbackOverlap).");
        }

        return ValidateOptionsResult.Success;
    }
}
