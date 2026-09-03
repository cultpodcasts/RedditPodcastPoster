using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Episodes;

public class Episode
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(2)]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("posted")]
    [JsonPropertyOrder(40)]
    public bool Posted { get; set; }

    [JsonPropertyName("tweeted")]
    [JsonPropertyOrder(41)]
    public bool Tweeted { get; set; }

    /// <summary>
    /// Legacy Cosmos flag (<c>bluesky</c>). Populated only by deserialization of pre-migration
    /// documents. Do not set to <c>true</c> in application code — store
    /// <see cref="BlueskyPost"/> after a network post. Clear via
    /// <see cref="ClearBlueskyPostState"/>.
    /// </summary>
    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(42)]
    public bool? OldBlueskyPosted { get; set; }

    /// <summary>
    /// AT URI of the Bluesky post (<c>at://{did}/app.bsky.feed.post/{rkey}</c>).
    /// Stored as string because <see cref="Uri"/> rejects AT Protocol DIDs (colons).
    /// </summary>
    [JsonPropertyName("blueskyPost")]
    [JsonPropertyOrder(42)]
    public string? BlueskyPost { get; set; }

    /// <summary>
    /// Whether this episode is considered Bluesky-posted (legacy flag or stored AT URI).
    /// Not serialized — Cosmos must query <c>bluesky</c> / <c>blueskyPost</c> with
    /// <c>IS_DEFINED</c> (see <see cref="CosmosIsBlueskyPostedSql"/>).
    /// </summary>
    [JsonIgnore]
    public bool BlueskyPosted =>
        (OldBlueskyPosted.HasValue && OldBlueskyPosted.Value) || !string.IsNullOrWhiteSpace(BlueskyPost);

    /// <summary>
    /// Cosmos SQL: episode is Bluesky-posted. Do not use null-only checks — combine
    /// <c>IS_DEFINED</c> with value comparison.
    /// </summary>
    public const string CosmosIsBlueskyPostedSql =
        "((IS_DEFINED(e.bluesky) AND e.bluesky = true) OR (IS_DEFINED(e.blueskyPost) AND NOT IS_NULL(e.blueskyPost)))";

    /// <summary>
    /// Cosmos SQL: episode is not Bluesky-posted.
    /// </summary>
    public const string CosmosIsNotBlueskyPostedSql =
        "((NOT IS_DEFINED(e.bluesky) OR e.bluesky != true) AND (NOT IS_DEFINED(e.blueskyPost) OR IS_NULL(e.blueskyPost)))";

    /// <summary>
    /// Clears both the legacy flag and stored AT URI (e.g. after un-post).
    /// </summary>
    public void ClearBlueskyPostState()
    {
        BlueskyPost = null;
        OldBlueskyPosted = null;
    }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    /// <summary>
    /// Grouped platform ids. Source of truth for matching and reconstructable services.
    /// Leftover Cosmos <c>spotifyId</c>/<c>appleId</c>/<c>youTubeId</c> JSON is ignored on
    /// deserialize and omitted on serialize (wither).
    /// </summary>
    [JsonPropertyName("ids")]
    [JsonPropertyOrder(53)]
    public EpisodeIds? Ids { get; set; }

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    /// <summary>
    /// Subjects removed by a curator; indexer enrichment must not re-add these.
    /// </summary>
    [JsonPropertyName("removedSubjects")]
    [JsonPropertyOrder(71)]
    public List<string> RemovedSubjects { get; set; } = [];

    [JsonPropertyName("matches")]
    [JsonPropertyOrder(72)]
    public List<EpisodeSubjectMatch> Matches { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    /// <summary>
    /// Optional episode-level hashtag appended to Tweet/Bluesky posts (e.g. <c>#MyTag</c>).
    /// </summary>
    [JsonPropertyName("hashTag")]
    [JsonPropertyOrder(81)]
    public string? HashTag { get; set; }

    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(90)]
    public string? PodcastName { get; set; }

    [JsonPropertyName("podcastSearchTerms")]
    [JsonPropertyOrder(91)]
    public string? PodcastSearchTerms { get; set; }

    [JsonPropertyName("podcastLanguage")]
    [JsonPropertyOrder(92)]
    public string? PodcastLanguage { get; set; }

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(45)]
    public string? Language { get; set; }

    [JsonPropertyName("podcastMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? PodcastMetadataVersion { get; set; }

    [JsonPropertyName("podcastRemoved")]
    [JsonPropertyOrder(94)]
    public bool? PodcastRemoved { get; set; }

    /// <summary>
    /// Per-service watch/listen URL and artwork, keyed by <see cref="ServiceKeys"/> (or a host slug).
    /// Canonical adjacent storage. Leftover named <c>urls</c> / <c>images</c> JSON is ignored on
    /// deserialize and omitted on serialize (wither).
    /// </summary>
    [JsonPropertyName("services")]
    [JsonPropertyOrder(151)]
    public Dictionary<string, EpisodeServiceLink>? Services { get; set; }

    [JsonPropertyName("guests")]
    [JsonPropertyOrder(160)]
    public string[]? Guests { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }

    public static Episode FromSpotify(string spotifyId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri spotifyUrl,
        Uri? maxImage)
    {
        var episode = new Episode
        {
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release
        };
        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, spotifyUrl, maxImage);
        return episode;
    }

    public static Episode FromYouTube(
        string youTubeId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri youTubeUrl,
        Uri? image)
    {
        var episode = new Episode
        {
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release
        };
        EpisodeServicePresence.SetYouTubeIdentity(episode, youTubeId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, youTubeUrl, image);
        return episode;
    }

    public static Episode FromApple(
        long appleId,
        string title,
        string description,
        TimeSpan length,
        bool @explicit,
        DateTime release,
        Uri url,
        Uri? image)
    {
        var episode = new Episode
        {
            Title = title,
            Description = description,
            Length = length,
            Explicit = @explicit,
            Release = release
        };
        EpisodeServicePresence.SetAppleIdentity(episode, appleId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, url, image);
        return episode;
    }

    /// <param name="inheritLanguageIfUnset">
    /// When true, copy <see cref="Podcast.Language"/> onto this episode if
    /// <see cref="Language"/> is unset. Used for <b>new episode create/merge</b> so a show default
    /// stamps onto a freshly created episode. Do <b>not</b> use this for podcast API language
    /// changes — use <see cref="ApplyPodcastDefaultLanguageChange"/> (null means English, not unset).
    /// See docs/episode-language.md.
    /// </param>
    public (bool, bool) SetPodcastProperties(Podcast podcast, bool inheritLanguageIfUnset = false)
    {
        var updated = false;
        if (PodcastId != podcast.Id)
        {
            PodcastId = podcast.Id;
            updated = true;
        }

        var podcastName = podcast.Name.Trim();
        if (PodcastName != podcastName)
        {
            PodcastName = podcastName;
            updated = true;
        }

        if (PodcastRemoved != podcast.Removed)
        {
            PodcastRemoved = podcast.Removed;
            updated = true;
        }

        var podcastSearchTerms = podcast.SearchTerms?.Trim();
        if (PodcastSearchTerms != podcastSearchTerms)
        {
            PodcastSearchTerms = podcastSearchTerms;
            updated = true;
        }

        var podcastLanguage = podcast.Language?.Trim();
        if (PodcastLanguage != podcastLanguage)
        {
            PodcastLanguage = podcastLanguage;
            updated = true;
        }

        var updatedMetadata = false;
        if (PodcastMetadataVersion != podcast.Timestamp)
        {
            PodcastMetadataVersion = podcast.Timestamp;
            updatedMetadata = true;
        }

        if (inheritLanguageIfUnset && InheritLanguageFromPodcastIfUnset(podcast))
        {
            updated = true;
        }

        return (updated, updatedMetadata);
    }

    public bool InheritLanguageFromPodcastIfUnset(Podcast podcast)
    {
        if (!string.IsNullOrWhiteSpace(Language))
        {
            return false;
        }

        var podcastLanguage = podcast.Language?.Trim();
        if (string.IsNullOrWhiteSpace(podcastLanguage))
        {
            return false;
        }

        Language = podcastLanguage;
        return true;
    }

    /// <summary>
    /// Podcast API default-language change: move this episode only if it still follows
    /// <paramref name="previousPodcastLanguage"/>. Null episode language is English (override when
    /// the previous default was non-English), not “unset”. See docs/episode-language.md.
    /// </summary>
    /// <returns>True when <see cref="Language"/> changed.</returns>
    public bool ApplyPodcastDefaultLanguageChange(
        string? previousPodcastLanguage,
        string? newPodcastLanguage)
    {
        var next = EpisodeLanguageResolution.LanguageAfterPodcastDefaultChange(
            Language,
            previousPodcastLanguage,
            newPodcastLanguage);
        var currentStored = EpisodeLanguageResolution.ToStoredLanguage(Language);
        if (string.Equals(currentStored, next, StringComparison.OrdinalIgnoreCase) ||
            (currentStored is null && next is null))
        {
            return false;
        }

        Language = next;
        return true;
    }

    /// <summary>
    /// Applies a curator subject update and maintains <see cref="RemovedSubjects"/>.
    /// </summary>
    public bool ApplyUserSubjects(IEnumerable<string> newSubjects)
    {
        var newList = newSubjects.ToList();
        if (Subjects.SequenceEqual(newList))
        {
            return false;
        }

        var newSet = new HashSet<string>(newList, StringComparer.OrdinalIgnoreCase);
        foreach (var subject in Subjects)
        {
            if (!newSet.Contains(subject) &&
                !RemovedSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase))
            {
                RemovedSubjects.Add(subject);
            }
        }

        RemovedSubjects.RemoveAll(s => newSet.Contains(s));
        Subjects = newList;
        Matches.RemoveAll(m => !newSet.Contains(m.Subject, StringComparer.OrdinalIgnoreCase));
        return true;
    }

    public bool IsSubjectRemovedByUser(string subjectName) =>
        RemovedSubjects.Contains(subjectName, StringComparer.OrdinalIgnoreCase);
}