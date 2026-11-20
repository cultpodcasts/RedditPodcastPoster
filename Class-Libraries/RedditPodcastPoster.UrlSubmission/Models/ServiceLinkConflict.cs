namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// Curator answer when a submitted Spotify / Apple URL matches a stored episode that already carries a different
/// id for that service (#1416). The default, <see cref="CreateNew"/>, is the behaviour before #1416 and is what
/// every caller without a curator prompt (console, discovery) keeps.
/// </summary>
public enum ServiceLinkConflictChoice
{
    /// <summary>File the submitted URL as a new episode and leave the stored episode as it is.</summary>
    CreateNew = 0,

    /// <summary>Write nothing and return a <see cref="ServiceLinkConflictHold"/> for the curator to answer.</summary>
    Prompt,

    /// <summary>
    /// Put the submitted links on the stored episode. Its old links are split off into a new episode of their
    /// own, so nothing is lost.
    /// </summary>
    Overwrite,

    /// <summary>Write nothing.</summary>
    Cancel
}

public static class ServiceLinkConflictChoices
{
    /// <summary>
    /// Wire value of a curator answer: <c>overwrite</c>, <c>create-new</c> or <c>cancel</c>. Absent (the first
    /// submit) is <see cref="ServiceLinkConflictChoice.Prompt"/>.
    /// </summary>
    /// <returns><see langword="false"/> for any other value.</returns>
    public static bool TryParseAnswer(string? value, out ServiceLinkConflictChoice choice)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "":
                choice = ServiceLinkConflictChoice.Prompt;
                return true;
            case "overwrite":
                choice = ServiceLinkConflictChoice.Overwrite;
                return true;
            case "create-new":
                choice = ServiceLinkConflictChoice.CreateNew;
                return true;
            case "cancel":
                choice = ServiceLinkConflictChoice.Cancel;
                return true;
            default:
                choice = ServiceLinkConflictChoice.Prompt;
                return false;
        }
    }
}

/// <summary>
/// Nothing was written: the submitted URL matches stored episode <see cref="EpisodeId"/>, which already carries a
/// different link for each of <see cref="Links"/>. Answer with <see cref="ServiceLinkConflictChoice"/> and this
/// <see cref="EpisodeId"/>.
/// </summary>
public sealed record ServiceLinkConflictHold(
    Guid EpisodeId,
    string EpisodeTitle,
    DateTime EpisodeReleaseUtc,
    IReadOnlyList<ServiceLinkConflictLink> Links);

/// <param name="Service">Service key (<c>spotify</c> or <c>apple</c>).</param>
/// <param name="StoredUrl">The link the stored episode has now (split off on overwrite).</param>
/// <param name="SubmittedUrl">The link the submit resolved for the same service.</param>
public sealed record ServiceLinkConflictLink(string Service, Uri? StoredUrl, Uri? SubmittedUrl);
