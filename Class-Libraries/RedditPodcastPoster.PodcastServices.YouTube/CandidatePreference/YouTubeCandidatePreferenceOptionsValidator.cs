using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>Validates <see cref="YouTubeCandidatePreferenceOptions"/> data annotations on start.</summary>
public class YouTubeCandidatePreferenceOptionsValidator : IValidateOptions<YouTubeCandidatePreferenceOptions>
{
    public ValidateOptionsResult Validate(string? name, YouTubeCandidatePreferenceOptions options)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true))
        {
            return ValidateOptionsResult.Fail(results.Select(r => r.ErrorMessage ?? "Invalid youTubeCandidatePreference option."));
        }

        return options.PenalisedTitleTokens == null
            ? ValidateOptionsResult.Fail("youTubeCandidatePreference:PenalisedTitleTokens must not be null.")
            : ValidateOptionsResult.Success;
    }
}