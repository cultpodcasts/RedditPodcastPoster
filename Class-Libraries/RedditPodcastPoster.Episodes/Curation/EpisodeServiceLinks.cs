using System.Globalization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Curation;

/// <summary>
/// Curator operation: move service links (the <c>services.{key}</c> url and image, and the nested
/// <c>ids.{spotify|apple|youtube}</c> id) from one podcast episode to another. Nothing is persisted here; the
/// caller saves both episodes.
/// <para>
/// Shared by submit's overwrite-and-split (#1416), where the stored links move onto a new episode so the
/// submitted links can take their place, and by the planned curator merge of duplicate episodes (#1471), which is
/// the inverse: links move from the duplicates onto the kept episode.
/// </para>
/// </summary>
public static class EpisodeServiceLinks
{
    /// <summary>
    /// Moves each of <paramref name="serviceKeys"/> from <paramref name="source"/> to <paramref name="target"/>
    /// and clears it on the source. A key the source does not carry is ignored. A key the target already holds
    /// with a different id or url is left on both episodes and reported in
    /// <see cref="EpisodeServiceLinkMove.Conflicting"/>, so no link is ever overwritten or lost.
    /// </summary>
    public static EpisodeServiceLinkMove Move(Episode source, Episode target, IReadOnlyCollection<string> serviceKeys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(serviceKeys);
        if (ReferenceEquals(source, target) || source.Id == target.Id)
        {
            throw new ArgumentException("Service links cannot be moved onto the same episode.", nameof(target));
        }

        var moved = new List<string>();
        var conflicting = new List<string>();
        foreach (var key in serviceKeys.Distinct(StringComparer.Ordinal))
        {
            var link = Read(source, key);
            if (link.IsEmpty)
            {
                continue;
            }

            var existing = Read(target, key);
            if (!existing.IsEmpty && !existing.SameAs(link))
            {
                conflicting.Add(key);
                continue;
            }

            Write(target, key, link);
            Clear(source, key);
            moved.Add(key);
        }

        return new EpisodeServiceLinkMove(moved, conflicting);
    }

    /// <summary>
    /// Splits <paramref name="serviceKeys"/> off <paramref name="source"/> into a new episode of the same podcast, so
    /// the links are kept when the source is about to take other links for those services (#1416 overwrite).
    /// The new episode starts as a copy of the source's details (title, description, release, length, language,
    /// subjects, matches, search terms, hashtag, guests and parent fields) for the curator to correct.
    /// <para>
    /// #1484: it is <b>hidden until a curator reviews and approves it</b> (<see cref="IsAwaitingApproval"/>): it is
    /// stored <see cref="Models.Catalogue.Playable.Removed"/>, the existing "not public" state (no public page,
    /// search, homepage or posting), and marked with <see cref="SplitOffTweetSkipReason"/>, which also keeps it off X
    /// for good. A curator approves it by clearing Removed in the episode editor. It is a recovery record and is
    /// never posted automatically (Reddit is marked posted; Bluesky has no skip reason, so its legacy manual-mark flag
    /// is set).
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The source carries none of <paramref name="serviceKeys"/>.</exception>
    public static EpisodeServiceLinkSplit SplitOff(Episode source, IReadOnlyCollection<string> serviceKeys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serviceKeys);
        var split = new Episode
        {
            PodcastId = source.PodcastId,
            PodcastName = source.PodcastName,
            Title = source.Title,
            Description = source.Description,
            Length = source.Length,
            Explicit = source.Explicit,
            Language = source.Language,
            Ignored = source.Ignored,
            Subjects = [..source.Subjects],
            RemovedSubjects = [..source.RemovedSubjects],
            Matches = source.Matches.Select(match => match.Clone()).ToList(),
            SearchTerms = source.SearchTerms,
            HashTag = source.HashTag,
            Guests = source.Guests is null ? null : [..source.Guests],
            PublisherSearchTerms = source.PublisherSearchTerms,
            PublisherLanguage = source.PublisherLanguage,
            ParentMetadataVersion = source.ParentMetadataVersion,
            ParentRemoved = source.ParentRemoved,
            Removed = true,
            Posted = true,
            TweetSkipReason = SplitOffTweetSkipReason,
            OldBlueskyPosted = true
        };
        split.ReleaseUtc = source.ReleaseUtc;

        var move = Move(source, split, serviceKeys);
        if (move.Moved.Count == 0)
        {
            throw new InvalidOperationException(
                $"Episode '{source.Id}' carries none of the service links '{string.Join("', '", serviceKeys)}' to split off.");
        }

        return new EpisodeServiceLinkSplit(split, move.Moved);
    }

    /// <summary>
    /// <c>tweetSkipReason</c> code on an episode created by <see cref="SplitOff"/> (#1484). It never goes to X, and
    /// while it is also <see cref="Models.Catalogue.Playable.Removed"/> it is awaiting curator approval.
    /// </summary>
    public const string SplitOffTweetSkipReason = "split-off";

    /// <summary>
    /// True for a split-off that a curator has not approved yet (#1484): still hidden
    /// (<see cref="Models.Catalogue.Playable.Removed"/>) and marked <see cref="SplitOffTweetSkipReason"/>. Such an
    /// episode is not public and is never matched by an ordinary submit, so a submit cannot enrich it instead of the
    /// stored episode it was split from.
    /// </summary>
    public static bool IsAwaitingApproval(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        return episode.Removed &&
               string.Equals(episode.TweetSkipReason, SplitOffTweetSkipReason, StringComparison.Ordinal);
    }

    private static ServiceLinkSnapshot Read(Episode episode, string key) =>
        new(
            IdOf(episode, key),
            EpisodeServicePresence.TryGetUrl(episode, key),
            EpisodeServicePresence.TryGetImage(episode, key));

    private static string? IdOf(Episode episode, string key) =>
        key switch
        {
            ServiceKeys.Spotify => EpisodeServicePresence.SpotifyEpisodeId(episode),
            ServiceKeys.Apple => EpisodeServicePresence.AppleEpisodeId(episode)?.ToString(CultureInfo.InvariantCulture),
            // Nested id only: YouTubeEpisodeId falls back to the url, which would count the url twice.
            ServiceKeys.YouTube => EpisodeServicePresence.NestedYouTubeId(episode),
            _ => null
        };

    private static void Write(Episode episode, string key, ServiceLinkSnapshot link)
    {
        SetId(episode, key, link.Id);
        if (link.Url is not null || link.Image is not null)
        {
            EpisodeServicePresence.Upsert(episode, key, link.Url, link.Image);
        }
    }

    private static void Clear(Episode episode, string key)
    {
        SetId(episode, key, null);
        EpisodeServicePresence.Upsert(episode, key, null, null);
    }

    private static void SetId(Episode episode, string key, string? id)
    {
        switch (key)
        {
            case ServiceKeys.Spotify:
                EpisodeServicePresence.SetSpotifyIdentity(episode, id);
                break;
            case ServiceKeys.Apple:
                EpisodeServicePresence.SetAppleIdentity(episode, id is null ? null : long.Parse(id, CultureInfo.InvariantCulture));
                break;
            case ServiceKeys.YouTube:
                EpisodeServicePresence.SetYouTubeIdentity(episode, id);
                break;
        }
    }

    private sealed record ServiceLinkSnapshot(string? Id, Uri? Url, Uri? Image)
    {
        public bool IsEmpty => Id is null && Url is null;

        /// <summary>The same link: equal ids when both carry one, otherwise equal urls.</summary>
        public bool SameAs(ServiceLinkSnapshot other) =>
            Id is not null && other.Id is not null
                ? string.Equals(Id, other.Id, StringComparison.Ordinal)
                : Url is not null && other.Url is not null && Url == other.Url;
    }
}

/// <summary>Outcome of <see cref="EpisodeServiceLinks.Move"/>.</summary>
/// <param name="Moved">Service keys now on the target and cleared from the source.</param>
/// <param name="Conflicting">Service keys left in place because the target holds a different link.</param>
public sealed record EpisodeServiceLinkMove(IReadOnlyList<string> Moved, IReadOnlyList<string> Conflicting);

/// <summary>Outcome of <see cref="EpisodeServiceLinks.SplitOff"/>.</summary>
/// <param name="Episode">The new episode holding the split-off links. Not yet saved.</param>
/// <param name="Moved">Service keys moved onto <paramref name="Episode"/>.</param>
public sealed record EpisodeServiceLinkSplit(Episode Episode, IReadOnlyList<string> Moved);
