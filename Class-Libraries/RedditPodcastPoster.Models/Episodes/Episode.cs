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
    /// Cosmos range filters must dual-key <see cref="Playable.ReleaseSort"/> with
    /// <see cref="Playable.ReleaseCosmosFallback"/> (legacy <c>release</c>) until backfill —
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
    /// Legacy Cosmos key. Read-only deserialize bridge into <see cref="Playable.PublisherSearchTerms"/>;
    /// omitted on serialize (<c>get</c> always null + WhenWritingNull).
    /// <b>Deserialize-only — not query-safe:</b> Cosmos SQL/LINQ that targets
    /// <c>publisherSearchTerms</c> ignores this key; dual-read
    /// <c>(publisherSearchTerms ?? podcastSearchTerms)</c> until corpus rewrite.
    /// Remove once episodes are rewritten without <c>podcastSearchTerms</c>.
    /// </summary>
    [JsonPropertyName("podcastSearchTerms")]
    [JsonPropertyOrder(91)]
    public string? LegacyPodcastSearchTerms
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(PublisherSearchTerms))
            {
                PublisherSearchTerms = value;
            }
        }
    }

    /// <summary>
    /// Legacy Cosmos key. Read-only deserialize bridge into <see cref="Playable.PublisherLanguage"/>;
    /// omitted on serialize (<c>get</c> always null + WhenWritingNull).
    /// <b>Deserialize-only — not query-safe:</b> Cosmos filters on <c>publisherLanguage</c>
    /// ignore this key until corpus rewrite. Remove once episodes are rewritten without
    /// <c>podcastLanguage</c>.
    /// </summary>
    [JsonPropertyName("podcastLanguage")]
    [JsonPropertyOrder(92)]
    public string? LegacyPodcastLanguage
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(PublisherLanguage))
            {
                PublisherLanguage = value;
            }
        }
    }

    /// <summary>
    /// Legacy Cosmos key. Read-only deserialize bridge into <see cref="Playable.ParentMetadataVersion"/>;
    /// omitted on serialize.
    /// <b>Deserialize-only — not query-safe:</b> Cosmos filters on <c>parentMetadataVersion</c>
    /// ignore this key until corpus rewrite. Remove once episodes are rewritten without
    /// <c>podcastMetadataVersion</c>.
    /// </summary>
    [JsonPropertyName("podcastMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? LegacyPodcastMetadataVersion
    {
        get => null;
        set
        {
            if (value.HasValue && !ParentMetadataVersion.HasValue)
            {
                ParentMetadataVersion = value;
            }
        }
    }

    /// <summary>
    /// Legacy Cosmos key. Read-only deserialize bridge into <see cref="Playable.ParentRemoved"/>;
    /// omitted on serialize.
    /// <b>Deserialize-only — not query-safe:</b> Cosmos SQL/LINQ that targets only
    /// <c>parentRemoved</c> treats <c>podcastRemoved</c>-only documents as not-removed
    /// (including <c>podcastRemoved: true</c>). Use <see cref="Playable.CosmosParentNotRemovedSql"/>
    /// / dual-key LINQ until a verified corpus rewrite. Do not remove this bridge until then.
    /// </summary>
    [JsonPropertyName("podcastRemoved")]
    [JsonPropertyOrder(94)]
    public bool? LegacyPodcastRemoved
    {
        get => null;
        set
        {
            if (value.HasValue && !ParentRemoved.HasValue)
            {
                ParentRemoved = value;
            }
        }
    }

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

        if (AssignPublisherDescription(podcast.Description))
        {
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

    /// <summary>
    /// Applies a curator subject update and maintains <see cref="Playable.RemovedSubjects"/>.
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
