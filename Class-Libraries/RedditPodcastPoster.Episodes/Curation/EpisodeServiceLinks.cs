using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
    /// #1484: it <b>awaits curator approval</b> (<see cref="Episode.ApprovalStatus"/> =
    /// <see cref="ApprovalStatus.AwaitingApproval"/>). While held (<see cref="Episode.IsHeldForApproval"/>) it is kept
    /// out of public pages, search, D1, the homepage and posting, and a submit matches it only by one of its own service
    /// ids and never offers it as a service-link conflict candidate. <see cref="Models.Catalogue.Playable.Removed"/> is
    /// also set so storage-level <c>removed</c> filters (Cosmos SQL, D1, reconcile) agree. Its posting flags are left
    /// clear: posting keys off the status, so once a curator approves it (<see cref="Approve"/>) it is posted under the
    /// usual rules; a rejected one (<see cref="Reject"/>) never is.
    /// </para>
    /// <para>
    /// #1482: its id is deterministic (<see cref="SplitOffId"/>: a name-based v5 GUID of the source id and its stored
    /// Spotify and Apple links), so an overwrite retried after a failed save gets the same id. A caller that finds an
    /// episode with that id already stored must use <see cref="SplitOffOnto"/> instead, so the saved split-off (and
    /// any curator edits to it, including an approval or rejection) is reused rather than rebuilt.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The source carries none of <paramref name="serviceKeys"/>.</exception>
    public static EpisodeServiceLinkSplit SplitOff(Episode source, IReadOnlyCollection<string> serviceKeys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serviceKeys);
        var split = new Episode
        {
            Id = SplitOffId(source),
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
            ApprovalStatus = ApprovalStatus.AwaitingApproval
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
    /// #1484: a curator approves a split-off awaiting approval (or one rejected earlier) by clearing its
    /// <see cref="Episode.ApprovalStatus"/> to null (null means approved). It becomes an ordinary public episode
    /// (Removed cleared) and the posting jobs may post it under their usual rules (release window, ignored, delayed
    /// YouTube publishing). Nothing is posted here and no posting flag is touched: a split-off is created with none set
    /// and is never posted while held. Approving an episode that is already approved (null) changes nothing.
    /// </summary>
    /// <returns>True when anything changed.</returns>
    public static bool Approve(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.ApprovalStatus is null)
        {
            return false;
        }

        episode.ApprovalStatus = null;
        episode.Removed = false;
        return true;
    }

    /// <summary>
    /// #1484: a curator rejects a split-off awaiting approval. It becomes <see cref="ApprovalStatus.Rejected"/>, stays
    /// hidden (Removed) and is never posted. Rejecting a rejected split-off changes nothing. An approved episode (null)
    /// cannot be rejected: it is indistinguishable from any ordinary episode, which is hidden with Removed instead.
    /// </summary>
    /// <returns>True when anything changed.</returns>
    /// <exception cref="InvalidOperationException">The episode is approved (null status), not awaiting approval.</exception>
    public static bool Reject(Episode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.ApprovalStatus is null)
        {
            throw new InvalidOperationException($"Episode '{episode.Id}' is not awaiting approval.");
        }

        if (episode.ApprovalStatus == ApprovalStatus.Rejected && episode.Removed)
        {
            return false;
        }

        episode.ApprovalStatus = ApprovalStatus.Rejected;
        episode.Removed = true;
        return true;
    }

    /// <summary>Namespace of <see cref="SplitOffId"/> (#1482).</summary>
    public static readonly Guid SplitOffIdNamespace = Guid.Parse("6f0d2c4e-91a7-4b3d-8e55-2a7c9b1f4d08");

    /// <summary>The links that name a split-off (<see cref="SplitOffId"/>), whatever subset is being split off.</summary>
    private static readonly string[] SplitOffIdKeys = [ServiceKeys.Apple, ServiceKeys.Spotify];

    /// <summary>
    /// The id of the episode split off <paramref name="source"/>: a name-based (RFC 4122 v5, SHA-1) GUID of the
    /// source id and its stored Spotify and Apple links (id, or url when there is no id), <b>always both</b>, whatever
    /// keys conflict on a given attempt. A secondary Apple resolve can succeed on one attempt and not the next, and
    /// the stored links only change once the stored episode's save succeeds, so the id is stable across a retried
    /// overwrite (#1482). Shared by <see cref="SplitOff"/> and the submit conflict gate, which uses it to recognise
    /// an already-saved split-off. Ordinary episodes have random (v4) ids, so only a split-off can carry this id.
    /// </summary>
    public static Guid SplitOffId(Episode source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var name = new StringBuilder(source.Id.ToString("D"));
        foreach (var key in SplitOffIdKeys)
        {
            var link = Read(source, key);
            if (!link.IsEmpty)
            {
                name.Append('|').Append(key).Append('=').Append(link.Id ?? link.Url!.ToString());
            }
        }

        return NameBasedGuid(SplitOffIdNamespace, name.ToString());
    }

    /// <summary>
    /// #1482: finishes an interrupted split onto <paramref name="existing"/>, the split-off already saved for
    /// <paramref name="source"/> (its id is <see cref="SplitOffId"/>). The existing episode's details, including any
    /// curator edits, are kept. For each Spotify / Apple key that <paramref name="existing"/> already holds with the
    /// same link, the link is cleared from <paramref name="source"/>, which completes the earlier move even if that
    /// key does not conflict on this attempt. Each of <paramref name="serviceKeys"/> it does not hold yet is moved
    /// onto it (<see cref="EpisodeServiceLinkSplit.SplitOffChanged"/> is then true and it must be saved). A key it
    /// holds with a different link is left on the source.
    /// </summary>
    public static EpisodeServiceLinkSplit SplitOffOnto(
        Episode source,
        Episode existing,
        IReadOnlyCollection<string> serviceKeys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(serviceKeys);
        if (existing.Id != SplitOffId(source))
        {
            throw new ArgumentException(
                $"Episode '{existing.Id}' is not the split-off of episode '{source.Id}'.", nameof(existing));
        }

        var moved = new List<string>();
        var changed = false;
        foreach (var key in SplitOffIdKeys.Union(serviceKeys, StringComparer.Ordinal))
        {
            var link = Read(source, key);
            if (link.IsEmpty)
            {
                continue;
            }

            var held = Read(existing, key);
            if (!held.IsEmpty && held.SameAs(link))
            {
                Clear(source, key);
                moved.Add(key);
            }
            else if (held.IsEmpty && serviceKeys.Contains(key, StringComparer.Ordinal))
            {
                Write(existing, key, link);
                Clear(source, key);
                moved.Add(key);
                changed = true;
            }
        }

        if (!serviceKeys.Any(key => moved.Contains(key, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Episode '{source.Id}' carries none of the service links '{string.Join("', '", serviceKeys)}' to split off onto '{existing.Id}'.");
        }

        return new EpisodeServiceLinkSplit(existing, moved, Reused: true, SplitOffChanged: changed);
    }

    private static Guid NameBasedGuid(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray(bigEndian: true);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);
        var hash = SHA1.HashData(input);
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
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
/// <param name="Episode">The episode holding the split-off links: new and not yet saved, or the reused split-off.</param>
/// <param name="Moved">Service keys moved onto <paramref name="Episode"/> (or already on it and cleared from the source).</param>
/// <param name="Reused">The split-off already existed (<see cref="EpisodeServiceLinks.SplitOffOnto"/>, #1482).</param>
/// <param name="SplitOffChanged">The split-off must be saved: always for a new one; for a reused one only when a link
/// was added to it, so a curator's edits are never overwritten by a rebuild.</param>
public sealed record EpisodeServiceLinkSplit(
    Episode Episode,
    IReadOnlyList<string> Moved,
    bool Reused = false,
    bool SplitOffChanged = true);
