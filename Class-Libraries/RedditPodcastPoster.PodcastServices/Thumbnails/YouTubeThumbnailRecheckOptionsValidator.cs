using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.Thumbnails;

public class YouTubeThumbnailRecheckOptionsValidator : IValidateOptions<YouTubeThumbnailRecheckOptions>
{
    public ValidateOptionsResult Validate(string? name, YouTubeThumbnailRecheckOptions options)
    {
        var failures = new List<string>();
        if (options.RecheckWindowHours <= 0)
        {
            failures.Add(Failure(nameof(YouTubeThumbnailRecheckOptions.RecheckWindowHours)));
        }

        if (options.MaxStoredImageProbesPerRun <= 0)
        {
            failures.Add(Failure(nameof(YouTubeThumbnailRecheckOptions.MaxStoredImageProbesPerRun)));
        }

        if (options.MaxReResolvesPerRun <= 0)
        {
            failures.Add(Failure(nameof(YouTubeThumbnailRecheckOptions.MaxReResolvesPerRun)));
        }

        if (options.ProbeTimeoutSeconds <= 0 ||
            options.ProbeTimeoutSeconds > YouTubeThumbnailResolver.ProbeTimeout.TotalSeconds)
        {
            failures.Add(
                $"{nameof(YouTubeThumbnailRecheckOptions)}.{nameof(YouTubeThumbnailRecheckOptions.ProbeTimeoutSeconds)} must be > 0 and <= {YouTubeThumbnailResolver.ProbeTimeout.TotalSeconds} (the probe HTTP client timeout) " +
                $"(config key: {YouTubeThumbnailRecheckOptions.SectionName}__{nameof(YouTubeThumbnailRecheckOptions.ProbeTimeoutSeconds)}).");
        }

        if (options.RunBudgetSeconds <= 0)
        {
            failures.Add(Failure(nameof(YouTubeThumbnailRecheckOptions.RunBudgetSeconds)));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string Failure(string property) =>
        $"{nameof(YouTubeThumbnailRecheckOptions)}.{property} must be > 0 " +
        $"(config key: {YouTubeThumbnailRecheckOptions.SectionName}__{property}).";
}
