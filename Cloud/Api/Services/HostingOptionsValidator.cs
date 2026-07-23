using Microsoft.Extensions.Options;
using Api.Configuration;

namespace Api.Services;

public class HostingOptionsValidator : IValidateOptions<HostingOptions>
{
    public ValidateOptionsResult Validate(string? name, HostingOptions options)
    {
        if (options.UserRoles is null)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(HostingOptions)}.{nameof(HostingOptions.UserRoles)} must not be null (config key: hosting__UserRoles).");
        }

        if (options.UserRoles.Any(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(HostingOptions)}.{nameof(HostingOptions.UserRoles)} must not contain null or blank entries (config key: hosting__UserRoles).");
        }

        return ValidateOptionsResult.Success;
    }
}
