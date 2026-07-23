using Microsoft.Extensions.Options;
using Indexer.Models;

namespace Indexer.Services;

public class PosterOptionsValidator : IValidateOptions<PosterOptions>
{
    public ValidateOptionsResult Validate(string? name, PosterOptions options)
    {
        if (options.ReleasedDaysAgo <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PosterOptions)}.{nameof(PosterOptions.ReleasedDaysAgo)} must be > 0 (config key: poster__ReleasedDaysAgo).");
        }

        if (options.MaxPosts is { } maxPosts && maxPosts <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PosterOptions)}.{nameof(PosterOptions.MaxPosts)} must be > 0 when set (config key: poster__MaxPosts).");
        }

        return ValidateOptionsResult.Success;
    }
}
