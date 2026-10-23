using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Dtos;

public class SubmitUrlRequest
{
    public required Uri Url { get; set; }

    public Guid? PodcastId { get; set; }

    public string? PodcastName { get; set; }

    /// <summary>
    /// <c>NewsOrganisation</c> or <c>TvShow</c> (case-insensitive): file the URL under that parent by
    /// <see cref="ParentId"/> and never create a podcast. Not allowed together with the podcast fields.
    /// </summary>
    public string? ParentKind { get; set; }

    public Guid? ParentId { get; set; }

    /// <summary>The parent's name as the caller saw it: a check and a log aid only.</summary>
    public string? ParentName { get; set; }

    /// <summary>
    /// Worker-injected streaming metadata from StreamMeta KV after prepare/extract
    /// (<c>submitUsesPrefetchedMetaWhenCached</c>). Azure trusts the authenticated Worker
    /// caller — do not forward SPA-supplied meta on public submit bodies.
    /// </summary>
    public NonPodcastServiceItemMetaData? PrefetchedMeta { get; set; }

    /// <summary>
    /// Answer to an unsupported-language prompt: <c>add</c>, <c>continue</c>, or <c>cancel</c>.
    /// Absent on the first submit, which detects before anything is created.
    /// </summary>
    public string? LanguageDecision { get; set; }
}
