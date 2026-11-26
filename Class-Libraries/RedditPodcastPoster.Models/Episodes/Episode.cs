using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Episodes;

[CosmosSelector(ModelType.Episode)]
public class Episode : Playable
{
    public Episode()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.Episode;
    }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(2)]
    public Guid PodcastId { get; set; }

    /// <summary>
    /// DateTime view of <see cref="Playable.Release"/> for podcast matching and enrichment.
    /// Gets/sets via <see cref="Playable.ReleaseSort"/> and <see cref="CatalogueRelease.FromDateTimeUtc"/>.
    /// Cosmos range filters use <see cref="Playable.ReleaseSort"/> (JSON <c>releaseSort</c>) —
    /// not this property (JsonIgnore).
    /// </summary>
    [JsonIgnore]
    public DateTime ReleaseUtc
    {
        get => ReleaseSort;
        set => SetRelease(CatalogueRelease.FromDateTimeUtc(value));
    }

    /// <summary>
    /// Grouped platform ids. Source of truth for matching and reconstructable services.
    /// Leftover Cosmos <c>spotifyId</c>/<c>appleId</c>/<c>youTubeId</c> JSON is ignored on
    /// deserialize and omitted on serialize (wither).
    /// </summary>
    [JsonPropertyName("ids")]
    [JsonPropertyOrder(53)]
    public EpisodeIds? Ids { get; set; }

    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(90)]
    public string? PodcastName { get; set; }

    /// <summary>
    /// Set only on an episode that needs curator approval before it is public (#1484; today a split-off made by a
    /// submit overwrite): <see cref="RedditPodcastPoster.Models.Episodes.ApprovalStatus.AwaitingApproval"/> until a curator
    /// approves it (clears this to null) or rejects it (<see cref="RedditPodcastPoster.Models.Episodes.ApprovalStatus.Rejected"/>).
    /// <b>Null means approved</b>, so every existing episode is approved with no data change. While it is held
    /// (<see cref="IsHeldForApproval"/>) the episode is never public or posted. An approved episode later removed (e.g.
    /// unplayable) stays approved (null). Stored as the enum name, like the other stored enums.
    /// </summary>
    [JsonPropertyName("approvalStatus")]
    [JsonPropertyOrder(91)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ApprovalStatus? ApprovalStatus { get; set; }

    /// <summary>
    /// #1500: true when a curator deliberately removed this episode's image (an edit that cleared the image and left
    /// no other image). Public surfaces then show <b>no</b> art: the search index and D1 carry
    /// <see cref="ImageRemovedSearchToken"/> instead of <c>""</c>, and the homepage, public episode, share cards and
    /// social posts never fall back to the YouTube thumbnail. <b>Null means not removed</b>, so an episode with no stored
    /// image and no flag keeps the YouTube thumbnail fallback. Cleared when a curator sets an image again.
    /// </summary>
    [JsonPropertyName("imageRemoved")]
    [JsonPropertyOrder(95)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ImageRemoved { get; set; }

    /// <summary>
    /// #1500: search-index / D1 <c>image</c> value for an episode whose image a curator removed. One character that is
    /// neither a URL nor a compaction sigil (<c>y</c>/<c>s</c>/<c>a</c>), so clients that predate it expand it to nothing.
    /// </summary>
    public const string ImageRemovedSearchToken = "-";

    /// <summary>True when a curator deliberately removed this episode's image (#1500).</summary>
    public bool IsImageRemoved() => ImageRemoved == true;

    /// <summary>Cosmos SQL (alias <c>e</c>) for episodes awaiting curator approval (#1484, #1491).</summary>
    public const string CosmosAwaitingApprovalSql = "e.approvalStatus = 'AwaitingApproval'";

    /// <summary>True while a curator has not approved it yet (#1484).</summary>
    public bool IsAwaitingApproval() => ApprovalStatus == Episodes.ApprovalStatus.AwaitingApproval;

    /// <summary>
    /// True while it awaits approval or was rejected (#1484): it is kept out of public pages, search, D1, the homepage
    /// and posting, keyed off <see cref="ApprovalStatus"/> rather than <see cref="Catalogue.Playable.Removed"/>.
    /// </summary>
    public bool IsHeldForApproval() => ApprovalStatus.IsHeld();

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
            Explicit = @explicit
        };
        episode.ReleaseUtc = release;
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
            Explicit = @explicit
        };
        episode.ReleaseUtc = release;
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
            Explicit = @explicit
        };
        episode.ReleaseUtc = release;
        EpisodeServicePresence.SetAppleIdentity(episode, appleId);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, url, image);
        return episode;
    }

    /// <param name="inheritLanguageIfUnset">
    /// When true, copy <see cref="Podcast.Language"/> onto this episode if
    /// <see cref="Playable.Language"/> is unset. Used for <b>new episode create/merge</b> so a show default
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

        if (ParentRemoved != podcast.Removed)
        {
            ParentRemoved = podcast.Removed;
            updated = true;
        }

        var podcastSearchTerms = podcast.SearchTerms?.Trim();
        if (PublisherSearchTerms != podcastSearchTerms)
        {
            PublisherSearchTerms = podcastSearchTerms;
            updated = true;
        }

        var podcastLanguage = podcast.Language?.Trim();
        if (PublisherLanguage != podcastLanguage)
        {
            PublisherLanguage = podcastLanguage;
            updated = true;
        }

        var updatedMetadata = false;
        if (ParentMetadataVersion != podcast.Timestamp)
        {
            ParentMetadataVersion = podcast.Timestamp;
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
    /// <returns>True when <see cref="Playable.Language"/> changed.</returns>
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

    public bool IsSubjectRemovedByUser(string subjectName) =>
        RemovedSubjects.Contains(subjectName, StringComparer.OrdinalIgnoreCase);
}
