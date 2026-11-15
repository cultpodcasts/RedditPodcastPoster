using System.Globalization;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// Warning text for a console submit that was held for an identity choice.
/// The console has no identity picker, so a held film or show is not written;
/// this says so instead of leaving only a "None/None" result line.
/// </summary>
public static class SubmitUrlIdentityHoldNotice
{
    public static string? From(SubmitResult result)
    {
        if (result.IdentityHold is not { } hold)
        {
            return null;
        }

        var options = hold.Candidates.Count == 0
            ? "no candidates"
            : string.Join(
                "; ",
                hold.Candidates.Select(candidate =>
                    candidate.Year is { } year
                        ? $"{candidate.Title} ({year.ToString(CultureInfo.InvariantCulture)}) [{candidate.Key}]"
                        : $"{candidate.Title} [{candidate.Key}]"));
        return $"Skipped: {hold.Target} '{hold.Query}' needs a curator identity choice, so nothing was written. " +
               $"Candidates: {options}. Submit it from the site to pick one, or fill the ids by hand.";
    }
}
