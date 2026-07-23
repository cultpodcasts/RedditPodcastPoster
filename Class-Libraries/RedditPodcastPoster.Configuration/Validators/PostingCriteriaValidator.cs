using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Options;

namespace RedditPodcastPoster.Configuration.Validators;

public class PostingCriteriaValidator : IValidateOptions<PostingCriteria>
{
    public ValidateOptionsResult Validate(string? name, PostingCriteria options)
    {
        if (options.MinimumDuration <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PostingCriteria)}.{nameof(PostingCriteria.MinimumDuration)} must be > 00:00:00 (config key: postingCriteria__MinimumDuration).");
        }

        if (options.TweetDays < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PostingCriteria)}.{nameof(PostingCriteria.TweetDays)} must be >= 0 (config key: postingCriteria__TweetDays).");
        }

        if (options.RedditDays < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PostingCriteria)}.{nameof(PostingCriteria.RedditDays)} must be >= 0 (config key: postingCriteria__RedditDays).");
        }

        if (options.BlueSkyDays < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PostingCriteria)}.{nameof(PostingCriteria.BlueSkyDays)} must be >= 0 (config key: postingCriteria__BlueSkyDays).");
        }

        if (options.CategoriserDays < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(PostingCriteria)}.{nameof(PostingCriteria.CategoriserDays)} must be >= 0 (config key: postingCriteria__CategoriserDays).");
        }

        return ValidateOptionsResult.Success;
    }
}
