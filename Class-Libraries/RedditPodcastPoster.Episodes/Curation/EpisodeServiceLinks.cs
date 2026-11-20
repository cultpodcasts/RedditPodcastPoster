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
    /// subjects and parent fields) for the curator to correct, and is marked as already posted on every channel:
    /// it is a recovery record and is never posted automatically.
    /// <para>
    /// #1482: its id is deterministic (<see cref="SplitOffId"/>: a name-based v5 GUID of the source id and the links
    /// being split off), so an overwrite retried after a failed save rebuilds the same split-off and saving it again
    /// replaces the copy already saved instead of adding a second one.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The source carries none of <paramref name="serviceKeys"/>.</exception>
    public static EpisodeServiceLinkSplit SplitOff(Episode source, IReadOnlyCollection<string> serviceKeys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serviceKeys);
        var split = new Episode
        {
            Id = SplitOffId(source, serviceKeys),
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
            PublisherSearchTerms = source.PublisherSearchTerms,
            PublisherLanguage = source.PublisherLanguage,
            ParentMetadataVersion = source.ParentMetadataVersion,
            ParentRemoved = source.ParentRemoved,
            Posted = true,
            OldTweeted = true,
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

    /// <summary>Namespace of <see cref="SplitOffId"/> (#1482).</summary>
    public static readonly Guid SplitOffIdNamespace = Guid.Parse("6f0d2c4e-91a7-4b3d-8e55-2a7c9b1f4d08");

    /// <summary>
    /// The id <see cref="SplitOff"/> gives the episode split off <paramref name="source"/> for
    /// <paramref name="serviceKeys"/>: a name-based (RFC 4122 v5, SHA-1) GUID of the source id and, per key in
    /// ordinal order, the link's id (or url when it has no id). The same stored episode and the same links always
    /// give the same id, so a retried overwrite is idempotent (#1482). An episode already holding those links as
    /// the split-off of <paramref name="source"/> is recognised by comparing its id with this.
    /// </summary>
    public static Guid SplitOffId(Episode source, IReadOnlyCollection<string> serviceKeys)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(serviceKeys);
        var name = new StringBuilder(source.Id.ToString("D"));
        foreach (var key in serviceKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var link = Read(source, key);
            if (!link.IsEmpty)
            {
                name.Append('|').Append(key).Append('=').Append(link.Id ?? link.Url!.ToString());
            }
        }

        return NameBasedGuid(SplitOffIdNamespace, name.ToString());
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
/// <param name="Episode">The new episode holding the split-off links. Not yet saved.</param>
/// <param name="Moved">Service keys moved onto <paramref name="Episode"/>.</param>
public sealed record EpisodeServiceLinkSplit(Episode Episode, IReadOnlyList<string> Moved);
