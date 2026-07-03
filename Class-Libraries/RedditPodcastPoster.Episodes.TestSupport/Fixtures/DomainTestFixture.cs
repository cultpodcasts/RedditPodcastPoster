using AutoFixture;
using AutoFixture.Dsl;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Shared AutoFixture wrapper for episode-domain business-rule tests.
/// Owns specimen customizations, incident constants, and factory helpers.
/// </summary>
public sealed class DomainTestFixture
{
  /// <summary>Today at midnight UTC.</summary>
  public static DateTime UtcToday => DateTime.UtcNow.Date;

  /// <summary>Midnight UTC on a day <paramref name="days"/> before today.</summary>
  public static DateTime UtcDaysAgo(int days) => UtcToday.AddDays(-days);

  /// <summary>
  /// Midnight UTC on a day <paramref name="days"/> before today.
  /// Use for date-only platforms (Spotify catalogue) — never combine with a time-of-day.
  /// </summary>
  public static DateTime UtcDateDaysAgo(int days) => UtcToday.AddDays(-days);

  /// <summary>Midnight UTC on a day <paramref name="days"/> after today.</summary>
  public static DateTime UtcDaysFromNow(int days) => UtcToday.AddDays(days);

  /// <summary>
  /// UTC datetime on a day offset from today with an explicit time-of-day.
  /// For Apple and YouTube releases only — Spotify catalogue has no time component.
  /// </summary>
  public static DateTime UtcAtTime(int daysOffset, TimeSpan timeOfDay) =>
    UtcToday.AddDays(daysOffset).Add(timeOfDay);

  private readonly Fixture _fixture;

  public DomainTestFixture()
  {
    _fixture = new Fixture();
    CustomizeFixture();
  }

  /// <summary>Production-incident and regression GUIDs used by business-rule tests.</summary>
  public static class Incidents
  {
    public static readonly Guid CultsToConsciousnessPodcastId =
      Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200");

    public const string CultsToConsciousnessChannelId = "c2c-channel";

    public const string CultsToConsciousnessSpotifyShowId = "6oTbi9wKZ2czCvSwBKxxoH";

    public static readonly long CultsToConsciousnessYouTubePublicationOffsetTicks =
      TimeSpan.FromDays(-33).Add(TimeSpan.FromHours(-12)).Ticks;

    public static readonly Guid DefaultPodcastId =
      Guid.Parse("4672c845-15b4-4f88-bbff-567d521fe4a2");

    public static readonly Guid C2CAbuserEpisodeId =
      Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362");

    public static readonly Guid C2CNegativeDelayEpisodeId =
      Guid.Parse("53ba0c64-58a7-4292-b7fe-ba135d4d3160");

    public static readonly Guid C2COtoOwnerEpisodeId =
      Guid.Parse("1c804814-12ac-40c8-a223-88ab7c703d38");

    public static readonly Guid PostmormonExistingEpisodeId =
      Guid.Parse("086b02d5-9ec7-432e-8e57-9279d32374da");

    public static readonly Guid AmbiguousYouTubeOnlyEpisodeId =
      Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid AmbiguousAppleOnlyEpisodeId =
      Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly Guid EpisodeMatchRegexStoredEpisodeId =
      Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static readonly Guid PositiveDelayAudioEpisodeId =
      Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public const string C2CAbuserYouTubeId = "UsqC0L9He2g";

    public const string C2CAbuserTitle =
      "I Confronted My Ab*ser 30 Years Later. Everything Changed";

    public const string C2CAbuserSpotifyIncomingTitle =
      "I Confronted My Abuser 30 Years Later… Everything Changed";

    public const string C2CAbuserSpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4";

    /// <summary>Days before today for the C2C abuser YouTube publish (production: 2026-06-04).</summary>
    public const int C2CAbuserYouTubeReleaseDaysAgo = 30;

    public static readonly TimeSpan C2CAbuserYouTubeReleaseTimeOfDay =
      new(13, 8, 6);

    /// <summary>Spotify catalogue lands this many calendar days after the YouTube publish day.</summary>
    public const int C2CAbuserSpotifyCatalogueDaysAfterYouTube = 28;

    public static DateTime C2CAbuserYouTubeRelease =>
      UtcAtTime(-C2CAbuserYouTubeReleaseDaysAgo, C2CAbuserYouTubeReleaseTimeOfDay);

    public static readonly TimeSpan C2CAbuserYouTubeLength = TimeSpan.Parse("01:28:37");

    public static readonly TimeSpan C2CAbuserSpotifyLength = TimeSpan.Parse("01:31:59.6990000");

    public static DateTime C2CAbuserSpotifyRelease =>
      UtcDateDaysAgo(C2CAbuserYouTubeReleaseDaysAgo - C2CAbuserSpotifyCatalogueDaysAfterYouTube);

    public const string C2CNegativeDelayTitle =
      "Why He Thinks Daughters Should Parent Their Siblings  (ft. Tia Levings)";

    public const string C2CNegativeDelayYouTubeId = "u6ZF-2sWQQc";

    public const int C2CNegativeDelayStoredDaysAgo = 34;

    public static readonly TimeSpan C2CNegativeDelayReleaseTimeOfDay = new(21, 15, 27);

    public const int C2CNegativeDelayIncomingDaysAfterStored = 28;

    public static DateTime C2CNegativeDelayRelease =>
      UtcAtTime(-C2CNegativeDelayStoredDaysAgo, C2CNegativeDelayReleaseTimeOfDay);

    public static readonly TimeSpan C2CNegativeDelayLength =
      TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(35);

    public const string C2CNegativeDelayIncomingSpotifyId = "1BTQKaev5KLjScdwHII14B";

    public const string C2CNegativeDelayIncomingTitle =
      "Becoming a Fundamentalist Trad Wife Almost Killed Me";

    public static DateTime C2CNegativeDelayIncomingRelease =>
      UtcDateDaysAgo(C2CNegativeDelayStoredDaysAgo - C2CNegativeDelayIncomingDaysAfterStored);

    public static readonly TimeSpan C2CNegativeDelayIncomingLength =
      TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(30);

    public const string OtoSpotifyId = "16LveQifI6eBwDXAINpd7G";

    public const string OtoTitle =
      "What Really Happens During \"Ordo Templi Orientis\" Initiations?  (Trapped in a Secret Society)";

    public const string OtoYouTubeId = "l3aIdJeg0vE";

    public const int OtoReleaseDaysAgo = 45;

    public static readonly TimeSpan OtoReleaseTimeOfDay = new(22, 15, 16);

    public const int OtoIncomingSpotifyDaysAfterRelease = 35;

    public static DateTime OtoRelease =>
      UtcAtTime(-OtoReleaseDaysAgo, OtoReleaseTimeOfDay);

    public static readonly TimeSpan OtoLength =
      TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(42);

    public static DateTime OtoIncomingSpotifyRelease =>
      UtcDateDaysAgo(OtoReleaseDaysAgo - OtoIncomingSpotifyDaysAfterRelease);

    public const string PostmormonPodcastName = "Postmormon Postmortem";

    public const string PostmormonStoredTitle =
      "The Bear River Massacre and the Mormon History Behind Washakie Ward";

    public const string PostmormonIncomingYouTubeTitle =
      "The Bear River Masscare and the Mormon History Behind the Washakie Ward";

    public const string PostmormonSpotifyId = "1UncRhHtmojlTq2mO0Gntz";

    public const string PostmormonYouTubeId = "l_iHjZWIsXw";

    public const int PostmormonReleaseDaysAgo = 2;

    public static readonly TimeSpan PostmormonReleaseTimeOfDay = TimeSpan.FromHours(12);

    public static DateTime PostmormonRelease =>
      UtcAtTime(-PostmormonReleaseDaysAgo, PostmormonReleaseTimeOfDay);

    public static readonly TimeSpan PostmormonStoredLength = TimeSpan.FromSeconds(878.503);

    public static readonly TimeSpan PostmormonIncomingLength =
      TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(39);

    public const string AmbiguousSharedTitle = "Shared episode title";

    public const string AmbiguousIncomingSpotifyId = "incomingSpotifyId01";

    public const long AmbiguousAppleId = 1234567890L;

    public const string PositiveDelayAudioTitle = "Episode A";

    public const string PositiveDelayIncomingYouTubeId = "delayedYouTube01";

    public const string PositiveDelayAudioSpotifyId = "delayedAudio01";

    public const string EpisodeMatchRegexStoredTitle = "#42 Stored episode about the first topic";

    public const string EpisodeMatchRegexDiscoveredTitle =
      "#42 Catalogue title with completely different wording";

    public const string EpisodeMatchRegexSpotifyId = "regexForcedSpotify01";
  }

  public Uri DefaultSpotifyUrl(string spotifyEpisodeId) =>
    new($"https://open.spotify.com/episode/{spotifyEpisodeId}");

  public Uri DefaultYouTubeUrl(string youTubeId) =>
    new($"https://www.youtube.com/watch?v={youTubeId}");

  public Uri DefaultAppleUrl(long appleId) =>
    new($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");

  public Uri DefaultSpotifyImage(string spotifyId) =>
    new($"https://i.scdn.co/image/test-{spotifyId}");

  public Uri DefaultYouTubeImage(string youTubeId) =>
    new($"https://i.ytimg.com/vi/{youTubeId}/hqdefault.jpg");

  public Uri DefaultAppleImage(long appleId) =>
    new($"https://example.com/apple-art-{appleId}.jpg");

  public SpotifyCatalogueInputBuilder BuildSpotifyCatalogueInput() =>
    new(_fixture);

  /// <summary>
  /// Spotify catalogue release is date-only; any time-of-day is floored to midnight UTC.
  /// </summary>
  public SpotifyCatalogueInput CreateSpotifyCatalogueInput(
    Action<SpotifyCatalogueInputBuilder>? configure = null)
  {
    var builder = BuildSpotifyCatalogueInput();
    configure?.Invoke(builder);
    return builder.Create();
  }

  public AppleCatalogueInputBuilder BuildAppleCatalogueInput() =>
    new(_fixture);

  public AppleCatalogueInput CreateAppleCatalogueInput(
    Action<AppleCatalogueInputBuilder>? configure = null)
  {
    var builder = BuildAppleCatalogueInput();
    configure?.Invoke(builder);
    return builder.Create();
  }

  public YouTubeCatalogueInputBuilder BuildYouTubeCatalogueInput() =>
    new(_fixture);

  public YouTubeCatalogueInput CreateYouTubeCatalogueInput(
    Action<YouTubeCatalogueInputBuilder>? configure = null)
  {
    var builder = BuildYouTubeCatalogueInput();
    configure?.Invoke(builder);
    return builder.Create();
  }

  public ResolvedSpotifyItemInputBuilder BuildResolvedSpotifyItemInput() =>
    new(_fixture);

  /// <summary>
  /// Spotify resolved-item release is date-only; any time-of-day is floored to midnight UTC.
  /// </summary>
  public ResolvedSpotifyItemInput CreateResolvedSpotifyItemInput(
    Action<ResolvedSpotifyItemInputBuilder>? configure = null)
  {
    var builder = BuildResolvedSpotifyItemInput();
    configure?.Invoke(builder);
    return builder.Create();
  }

  public ResolvedAppleItemInputBuilder BuildResolvedAppleItemInput() =>
    new(_fixture);

  public ResolvedAppleItemInput CreateResolvedAppleItemInput(
    Action<ResolvedAppleItemInputBuilder>? configure = null)
  {
    var builder = BuildResolvedAppleItemInput();
    configure?.Invoke(builder);
    return builder.Create();
  }

  public ResolvedYouTubeItemInputBuilder BuildResolvedYouTubeItemInput() =>
    new(_fixture);

  public ResolvedYouTubeItemInput CreateResolvedYouTubeItemInput(
    Action<ResolvedYouTubeItemInputBuilder>? configure = null)
  {
    var builder = BuildResolvedYouTubeItemInput();
    configure?.Invoke(builder);
    return builder.Create();
  }

  public IFixture Auto => _fixture;

  public IPostprocessComposer<T> Build<T>() => _fixture.Build<T>();

  public T Create<T>() => _fixture.Create<T>();

  public Podcast CreatePodcast(Action<Podcast>? customize = null)
  {
    var podcast = new Podcast
    {
      Id = Guid.NewGuid(),
      Name = _fixture.Create<string>(),
      SpotifyId = string.Empty,
      YouTubeChannelId = string.Empty,
      YouTubePlaylistId = string.Empty
    };
    customize?.Invoke(podcast);
    return podcast;
  }

  public Podcast CreateSpotifyPrimaryPodcast(string spotifyShowId, Guid? id = null) =>
    CreatePodcast(p =>
    {
      if (id.HasValue)
        p.Id = id.Value;
      p.Name = "Spotify-primary podcast";
      p.SpotifyId = spotifyShowId;
      p.ReleaseAuthority = Service.Spotify;
    });

  public Podcast CreateYouTubeFirstPodcast(
    string channelId,
    long youTubePublicationOffsetTicks,
    string? spotifyShowId = null,
    Guid? id = null) =>
    CreatePodcast(p =>
    {
      p.Id = id ?? Incidents.CultsToConsciousnessPodcastId;
      p.Name = "YouTube-first podcast";
      p.ReleaseAuthority = Service.YouTube;
      p.YouTubeChannelId = channelId;
      p.YouTubePublicationOffset = youTubePublicationOffsetTicks;
      p.SpotifyId = spotifyShowId ?? string.Empty;
    });

  public Podcast CreateCultsToConsciousnessPodcast() =>
    CreateYouTubeFirstPodcast(
      Incidents.CultsToConsciousnessChannelId,
      Incidents.CultsToConsciousnessYouTubePublicationOffsetTicks,
      Incidents.CultsToConsciousnessSpotifyShowId,
      Incidents.CultsToConsciousnessPodcastId);

  public Episode CreateEpisode(Action<Episode>? customize = null)
  {
    var episode = new Episode
    {
      Id = Guid.NewGuid(),
      PodcastId = Incidents.DefaultPodcastId,
      Title = _fixture.Create<string>(),
      Description = _fixture.Create<string>(),
      Release = UtcDaysAgo(_fixture.Create<int>() % 365 + 1),
      Length = TimeSpan.FromMinutes(_fixture.Create<int>() % 120 + 1),
      Urls = new ServiceUrls(),
      Images = new EpisodeImages(),
      Subjects = [],
      SpotifyId = string.Empty,
      YouTubeId = string.Empty
    };
    customize?.Invoke(episode);
    return episode;
  }

  public Episode CreateStoredEpisode(Podcast podcast, Action<Episode>? customize = null) =>
    CreateEpisode(e =>
    {
      e.PodcastId = podcast.Id;
      customize?.Invoke(e);
    });

  public Episode CreateC2CYouTubeOnlyStoredEpisode(
    Podcast podcast,
    DateTime? release = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.C2CAbuserEpisodeId;
      e.Title = Incidents.C2CAbuserTitle;
      e.Release = release ?? Incidents.C2CAbuserYouTubeRelease;
      e.Length = length ?? Incidents.C2CAbuserYouTubeLength;
      e.YouTubeId = Incidents.C2CAbuserYouTubeId;
      e.Urls.YouTube = DefaultYouTubeUrl(Incidents.C2CAbuserYouTubeId);
    });

  public Episode CreateC2CYouTubeAuthorityStoredEpisode(
    Podcast podcast,
    DateTime? release = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.C2CAbuserEpisodeId;
      e.Title = Incidents.C2CAbuserTitle;
      e.Release = release ?? Incidents.C2CAbuserYouTubeRelease;
      e.Length = length ?? Incidents.C2CAbuserYouTubeLength;
      e.YouTubeId = Incidents.C2CAbuserYouTubeId;
      e.SpotifyId = Incidents.C2CAbuserSpotifyId;
      e.Urls.YouTube = DefaultYouTubeUrl(Incidents.C2CAbuserYouTubeId);
      e.Urls.Spotify = DefaultSpotifyUrl(Incidents.C2CAbuserSpotifyId);
    });

  public Episode CreateC2CSpotifyIncoming(
    DateTime? release = null,
    TimeSpan? length = null) =>
    CreateSpotifyCatalogueEpisode(
      Incidents.C2CAbuserSpotifyId,
      Incidents.C2CAbuserSpotifyIncomingTitle,
      DefaultSpotifyUrl(Incidents.C2CAbuserSpotifyId),
      release ?? Incidents.C2CAbuserSpotifyRelease,
      length ?? Incidents.C2CAbuserSpotifyLength);

  public Episode CreateC2CNegativeDelayStoredEpisode(Podcast podcast) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.C2CNegativeDelayEpisodeId;
      e.Title = Incidents.C2CNegativeDelayTitle;
      e.Release = Incidents.C2CNegativeDelayRelease;
      e.Length = Incidents.C2CNegativeDelayLength;
      e.YouTubeId = Incidents.C2CNegativeDelayYouTubeId;
      e.Urls.YouTube = DefaultYouTubeUrl(Incidents.C2CNegativeDelayYouTubeId);
    });

  public Episode CreateC2CNegativeDelaySpotifyIncoming() =>
    CreateSpotifyCatalogueEpisode(
      Incidents.C2CNegativeDelayIncomingSpotifyId,
      Incidents.C2CNegativeDelayIncomingTitle,
      DefaultSpotifyUrl(Incidents.C2CNegativeDelayIncomingSpotifyId),
      Incidents.C2CNegativeDelayIncomingRelease,
      Incidents.C2CNegativeDelayIncomingLength);

  public Episode CreateOtoCorrectOwnerEpisode(Podcast podcast) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.C2COtoOwnerEpisodeId;
      e.Title = Incidents.OtoTitle;
      e.Release = Incidents.OtoRelease;
      e.Length = Incidents.OtoLength;
      e.SpotifyId = Incidents.OtoSpotifyId;
      e.YouTubeId = Incidents.OtoYouTubeId;
      e.Urls.Spotify = DefaultSpotifyUrl(Incidents.OtoSpotifyId);
      e.Urls.YouTube = DefaultYouTubeUrl(Incidents.OtoYouTubeId);
    });

  public Episode CreateOtoWrongYouTubeOnlyEpisode(Podcast podcast) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.C2CNegativeDelayEpisodeId;
      e.Title = Incidents.C2CNegativeDelayTitle;
      e.Release = Incidents.C2CNegativeDelayRelease;
      e.Length = Incidents.C2CNegativeDelayLength;
      e.YouTubeId = Incidents.C2CNegativeDelayYouTubeId;
      e.Urls.YouTube = DefaultYouTubeUrl(Incidents.C2CNegativeDelayYouTubeId);
    });

  public Episode CreateOtoSpotifyIncoming() =>
    CreateSpotifyCatalogueEpisode(
      Incidents.OtoSpotifyId,
      spotifyUrl: DefaultSpotifyUrl(Incidents.OtoSpotifyId),
      release: Incidents.OtoIncomingSpotifyRelease,
      length: Incidents.OtoLength);

  public Podcast CreatePostmormonPodcast() =>
    CreatePodcast(p => p.Name = Incidents.PostmormonPodcastName);

  public Episode CreatePostmormonStoredEpisode(
    Podcast podcast,
    DateTime? release = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.PostmormonExistingEpisodeId;
      e.Title = Incidents.PostmormonStoredTitle;
      e.Release = release ?? Incidents.PostmormonRelease;
      e.Length = length ?? Incidents.PostmormonStoredLength;
      e.SpotifyId = Incidents.PostmormonSpotifyId;
      e.Urls.Spotify = DefaultSpotifyUrl(Incidents.PostmormonSpotifyId);
    });

  public Episode CreatePostmormonYouTubeIncoming(
    DateTime? release = null,
    TimeSpan? length = null) =>
    CreateYouTubeCatalogueEpisode(
      Incidents.PostmormonYouTubeId,
      Incidents.PostmormonIncomingYouTubeTitle,
      release ?? Incidents.PostmormonRelease,
      length ?? Incidents.PostmormonIncomingLength);

  public (Episode YouTubeOnly, Episode AppleOnly) CreateAmbiguousMatchStoredEpisodes(
    Podcast podcast,
    DateTime release,
    TimeSpan length)
  {
    var youTubeOnly = CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.AmbiguousYouTubeOnlyEpisodeId;
      e.Title = Incidents.AmbiguousSharedTitle;
      e.Release = release;
      e.Length = length;
      e.YouTubeId = "youtube-video-id";
      e.Urls.YouTube = DefaultYouTubeUrl("youtube-video-id");
    });
    var appleOnly = CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.AmbiguousAppleOnlyEpisodeId;
      e.Title = Incidents.AmbiguousSharedTitle;
      e.Release = release;
      e.Length = length;
      e.AppleId = Incidents.AmbiguousAppleId;
      e.Urls.Apple = DefaultAppleUrl(Incidents.AmbiguousAppleId);
    });
    return (youTubeOnly, appleOnly);
  }

  public Episode CreateAmbiguousMatchSpotifyIncoming(
    DateTime release,
    TimeSpan length) =>
    CreateSpotifyCatalogueEpisode(
      Incidents.AmbiguousIncomingSpotifyId,
      Incidents.AmbiguousSharedTitle,
      DefaultSpotifyUrl(Incidents.AmbiguousIncomingSpotifyId),
      release,
      length);

  public Episode CreatePositiveDelayAudioStoredEpisode(
    Podcast podcast,
    DateTime? audioRelease = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.PositiveDelayAudioEpisodeId;
      e.Title = Incidents.PositiveDelayAudioTitle;
      e.Release = audioRelease ?? UtcDaysAgo(_fixture.Create<int>() % 365 + 1);
      e.Length = length ?? TimeSpan.FromMinutes(_fixture.Create<int>() % 120 + 1);
      e.Urls.Spotify = DefaultSpotifyUrl(Incidents.PositiveDelayAudioSpotifyId);
    });

  public Episode CreateMidnightUtcSpotifyStoredEpisode(
    Podcast podcast,
    DateTime dateOnlyRelease,
    string? title = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Release = dateOnlyRelease;
      e.SpotifyId = Incidents.C2CAbuserSpotifyId;
      e.Urls.Spotify = DefaultSpotifyUrl(Incidents.C2CAbuserSpotifyId);
      if (title is not null)
        e.Title = title;
      if (length is not null)
        e.Length = length.Value;
    });

  public Episode CreateEpisodeMatchRegexStoredEpisode(
    Podcast podcast,
    DateTime? release = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Id = Incidents.EpisodeMatchRegexStoredEpisodeId;
      e.Title = Incidents.EpisodeMatchRegexStoredTitle;
      e.Release = release ?? UtcDaysAgo(_fixture.Create<int>() % 365 + 1);
      e.Length = TimeSpan.FromMinutes(30);
    });

  public Episode CreateEpisodeMatchRegexDiscoveredEpisode(DateTime? release = null) =>
    CreateSpotifyCatalogueEpisode(
      Incidents.EpisodeMatchRegexSpotifyId,
      Incidents.EpisodeMatchRegexDiscoveredTitle,
      DefaultSpotifyUrl(Incidents.EpisodeMatchRegexSpotifyId),
      release ?? UtcDaysAgo(_fixture.Create<int>() % 365 + 1),
      TimeSpan.FromMinutes(_fixture.Create<int>() % 120 + 1));

  public EpisodeBuilder BuildEpisode() => new(this);

  public Episode CreateSubmittedViaSpotifyUrlOnly(
    Uri spotifyUrl,
    string title = "Reddit post title",
    DateTime? release = null,
    Guid? podcastId = null,
    Guid? episodeId = null) =>
    CreateEpisode(e =>
    {
      if (episodeId.HasValue)
        e.Id = episodeId.Value;
      e.PodcastId = podcastId ?? Incidents.DefaultPodcastId;
      e.Title = title;
      e.Release = release ?? DateTime.UtcNow.Date;
      e.Urls.Spotify = spotifyUrl;
    });

  public Episode CreateSpotifyCatalogueEpisode(
    string spotifyId,
    string? title = null,
    Uri? spotifyUrl = null,
    DateTime? release = null,
    TimeSpan? length = null,
    string? description = null,
    Uri? image = null)
  {
    var input = CreateSpotifyCatalogueInput(b =>
    {
      b.WithSpotifyId(spotifyId);
      if (title is not null)
        b.WithTitle(title);
      if (spotifyUrl is not null)
        b.WithSpotifyUrl(spotifyUrl);
      if (release is null)
        b.WithRelease(DomainTestFixture.CreateSpotifyReleaseSpecimen(_fixture));
      if (length is not null)
        b.WithDuration(length.Value);
      if (description is not null)
        b.WithDescription(description);
      if (image is not null)
        b.WithImage(image);
    });
    return Episode.FromSpotify(
      input.SpotifyId,
      input.Title,
      input.Description,
      input.Duration,
      false,
      release ?? input.Release,
      input.SpotifyUrl,
      input.Image);
  }

  public Episode CreateYouTubeCatalogueEpisode(
    string youTubeId,
    string? title = null,
    DateTime? release = null,
    TimeSpan? length = null,
    string? description = null,
    Uri? youTubeUrl = null,
    Uri? image = null)
  {
    var input = CreateYouTubeCatalogueInput(b =>
    {
      b.WithYouTubeId(youTubeId);
      if (title is not null)
        b.WithTitle(title);
      if (release is not null)
        b.WithRelease(release.Value);
      if (length is not null)
        b.WithDuration(length.Value);
      if (description is not null)
        b.WithDescription(description);
      if (youTubeUrl is not null)
        b.WithYouTubeUrl(youTubeUrl);
      if (image is not null)
        b.WithImage(image);
    });
    return Episode.FromYouTube(
      input.YouTubeId,
      input.Title,
      input.Description,
      input.Duration,
      false,
      input.Release,
      input.YouTubeUrl,
      input.Image);
  }

  public Episode CreateAppleCatalogueEpisode(
    long appleId,
    string? title = null,
    DateTime? release = null,
    TimeSpan? length = null,
    string? description = null,
    Uri? appleUrl = null)
  {
    var input = CreateAppleCatalogueInput(b =>
    {
      b.WithAppleId(appleId);
      if (title is not null)
        b.WithTitle(title);
      if (release is not null)
        b.WithRelease(release.Value);
      if (length is not null)
        b.WithDuration(length.Value);
      if (description is not null)
        b.WithDescription(description);
      if (appleUrl is not null)
        b.WithAppleUrl(appleUrl);
    });
    return Episode.FromApple(
      input.AppleId,
      input.Title,
      input.Description,
      input.Duration,
      false,
      input.Release,
      input.AppleUrl,
      input.Image);
  }

  private void CustomizeFixture()
  {
    _fixture.Register(() => new Uri($"https://example.com/{_fixture.Create<Guid>()}"));
    _fixture.Customize<Podcast>(composer => composer.FromFactory(() => CreatePodcast()));
    _fixture.Customize<Episode>(composer => composer.FromFactory(() => CreateEpisode()));
  }

  internal static string CreateSpotifyIdSpecimen(Fixture fixture) =>
    fixture.Create<string>().Replace("-", "")[..22];

  internal static long CreateAppleIdSpecimen(Fixture fixture) =>
    Math.Abs(fixture.Create<long>()) % 9_000_000_000L + 1_000_000_000L;

  internal static string CreateYouTubeIdSpecimen(Fixture fixture) =>
    fixture.Create<string>().Replace("-", "")[..11];

  internal static TimeSpan CreateDurationSpecimen(Fixture fixture) =>
    TimeSpan.FromMinutes(fixture.Create<int>() % 120 + 1);

  internal static DateTime CreateSpotifyReleaseSpecimen(Fixture fixture) =>
    UtcDateDaysAgo(fixture.Create<int>() % 365 + 1);

  internal static DateTime CreateCatalogueReleaseSpecimen(Fixture fixture) =>
    UtcDaysAgo(fixture.Create<int>() % 365 + 1);
}

/// <summary>Fluent builder for Spotify catalogue input specimens — release is always date-only.</summary>
public sealed class SpotifyCatalogueInputBuilder
{
  private readonly Fixture _fixture;
  private string? _spotifyId;
  private string? _title;
  private string? _description;
  private TimeSpan? _duration;
  private DateTime? _release;
  private Uri? _spotifyUrl;
  private Uri? _image;

  internal SpotifyCatalogueInputBuilder(Fixture fixture) => _fixture = fixture;

  public SpotifyCatalogueInputBuilder WithSpotifyId(string spotifyId)
  {
    _spotifyId = spotifyId;
    return this;
  }

  public SpotifyCatalogueInputBuilder WithTitle(string title)
  {
    _title = title;
    return this;
  }

  public SpotifyCatalogueInputBuilder WithDescription(string description)
  {
    _description = description;
    return this;
  }

  public SpotifyCatalogueInputBuilder WithDuration(TimeSpan duration)
  {
    _duration = duration;
    return this;
  }

  public SpotifyCatalogueInputBuilder WithRelease(DateTime release)
  {
    _release = release.Date;
    return this;
  }

  public SpotifyCatalogueInputBuilder WithSpotifyUrl(Uri spotifyUrl)
  {
    _spotifyUrl = spotifyUrl;
    return this;
  }

  public SpotifyCatalogueInputBuilder WithImage(Uri? image)
  {
    _image = image;
    return this;
  }

  public SpotifyCatalogueInput Create()
  {
    var id = _spotifyId ?? DomainTestFixture.CreateSpotifyIdSpecimen(_fixture);
    return new SpotifyCatalogueInput(
      id,
      _title ?? _fixture.Create<string>(),
      _description ?? _fixture.Create<string>(),
      _duration ?? DomainTestFixture.CreateDurationSpecimen(_fixture),
      (_release ?? DomainTestFixture.CreateSpotifyReleaseSpecimen(_fixture)).Date,
      _spotifyUrl ?? new Uri($"https://open.spotify.com/episode/{id}"),
      _image);
  }
}

/// <summary>Fluent builder for Apple catalogue input specimens.</summary>
public sealed class AppleCatalogueInputBuilder
{
  private readonly Fixture _fixture;
  private long? _appleId;
  private string? _title;
  private string? _description;
  private TimeSpan? _duration;
  private DateTime? _release;
  private Uri? _appleUrl;
  private Uri? _image;

  internal AppleCatalogueInputBuilder(Fixture fixture) => _fixture = fixture;

  public AppleCatalogueInputBuilder WithAppleId(long appleId)
  {
    _appleId = appleId;
    return this;
  }

  public AppleCatalogueInputBuilder WithTitle(string title)
  {
    _title = title;
    return this;
  }

  public AppleCatalogueInputBuilder WithDescription(string description)
  {
    _description = description;
    return this;
  }

  public AppleCatalogueInputBuilder WithDuration(TimeSpan duration)
  {
    _duration = duration;
    return this;
  }

  public AppleCatalogueInputBuilder WithRelease(DateTime release)
  {
    _release = release;
    return this;
  }

  public AppleCatalogueInputBuilder WithAppleUrl(Uri appleUrl)
  {
    _appleUrl = appleUrl;
    return this;
  }

  public AppleCatalogueInputBuilder WithImage(Uri? image)
  {
    _image = image;
    return this;
  }

  public AppleCatalogueInput Create()
  {
    var id = _appleId ?? DomainTestFixture.CreateAppleIdSpecimen(_fixture);
    return new AppleCatalogueInput(
      id,
      _title ?? _fixture.Create<string>(),
      _description ?? _fixture.Create<string>(),
      _duration ?? DomainTestFixture.CreateDurationSpecimen(_fixture),
      _release ?? DomainTestFixture.CreateCatalogueReleaseSpecimen(_fixture),
      _appleUrl ?? new Uri($"https://podcasts.apple.com/us/podcast/episode/id{id}"),
      _image);
  }
}

/// <summary>Fluent builder for YouTube catalogue input specimens.</summary>
public sealed class YouTubeCatalogueInputBuilder
{
  private readonly Fixture _fixture;
  private string? _youTubeId;
  private string? _title;
  private string? _description;
  private TimeSpan? _duration;
  private DateTime? _release;
  private Uri? _youTubeUrl;
  private Uri? _image;

  internal YouTubeCatalogueInputBuilder(Fixture fixture) => _fixture = fixture;

  public YouTubeCatalogueInputBuilder WithYouTubeId(string youTubeId)
  {
    _youTubeId = youTubeId;
    return this;
  }

  public YouTubeCatalogueInputBuilder WithTitle(string title)
  {
    _title = title;
    return this;
  }

  public YouTubeCatalogueInputBuilder WithDescription(string description)
  {
    _description = description;
    return this;
  }

  public YouTubeCatalogueInputBuilder WithDuration(TimeSpan duration)
  {
    _duration = duration;
    return this;
  }

  public YouTubeCatalogueInputBuilder WithRelease(DateTime release)
  {
    _release = release;
    return this;
  }

  public YouTubeCatalogueInputBuilder WithYouTubeUrl(Uri youTubeUrl)
  {
    _youTubeUrl = youTubeUrl;
    return this;
  }

  public YouTubeCatalogueInputBuilder WithImage(Uri? image)
  {
    _image = image;
    return this;
  }

  public YouTubeCatalogueInput Create()
  {
    var id = _youTubeId ?? DomainTestFixture.CreateYouTubeIdSpecimen(_fixture);
    return new YouTubeCatalogueInput(
      id,
      _title ?? _fixture.Create<string>(),
      _description ?? _fixture.Create<string>(),
      _duration ?? DomainTestFixture.CreateDurationSpecimen(_fixture),
      _release ?? DomainTestFixture.CreateCatalogueReleaseSpecimen(_fixture),
      _youTubeUrl ?? new Uri($"https://www.youtube.com/watch?v={id}"),
      _image);
  }
}

/// <summary>Fluent builder for ResolvedSpotifyItem input specimens — release is always date-only.</summary>
public sealed class ResolvedSpotifyItemInputBuilder
{
  private readonly Fixture _fixture;
  private string? _episodeId;
  private string? _title;
  private string? _description;
  private DateTime? _release;
  private TimeSpan? _duration;
  private Uri? _url;
  private Uri? _image;

  internal ResolvedSpotifyItemInputBuilder(Fixture fixture) => _fixture = fixture;

  public ResolvedSpotifyItemInputBuilder WithEpisodeId(string episodeId)
  {
    _episodeId = episodeId;
    return this;
  }

  public ResolvedSpotifyItemInputBuilder WithTitle(string title)
  {
    _title = title;
    return this;
  }

  public ResolvedSpotifyItemInputBuilder WithDescription(string description)
  {
    _description = description;
    return this;
  }

  public ResolvedSpotifyItemInputBuilder WithRelease(DateTime release)
  {
    _release = release.Date;
    return this;
  }

  public ResolvedSpotifyItemInputBuilder WithDuration(TimeSpan duration)
  {
    _duration = duration;
    return this;
  }

  public ResolvedSpotifyItemInputBuilder WithUrl(Uri? url)
  {
    _url = url;
    return this;
  }

  public ResolvedSpotifyItemInputBuilder WithImage(Uri? image)
  {
    _image = image;
    return this;
  }

  public ResolvedSpotifyItemInput Create()
  {
    var id = _episodeId ?? DomainTestFixture.CreateSpotifyIdSpecimen(_fixture);
    return new ResolvedSpotifyItemInput(
      id,
      _title ?? _fixture.Create<string>(),
      _description ?? _fixture.Create<string>(),
      (_release ?? DomainTestFixture.CreateSpotifyReleaseSpecimen(_fixture)).Date,
      _duration ?? DomainTestFixture.CreateDurationSpecimen(_fixture),
      _url ?? new Uri($"https://open.spotify.com/episode/{id}"),
      _image);
  }
}

/// <summary>Fluent builder for ResolvedAppleItem input specimens.</summary>
public sealed class ResolvedAppleItemInputBuilder
{
  private readonly Fixture _fixture;
  private long? _episodeId;
  private string? _title;
  private string? _description;
  private DateTime? _release;
  private TimeSpan? _duration;
  private Uri? _url;
  private Uri? _image;

  internal ResolvedAppleItemInputBuilder(Fixture fixture) => _fixture = fixture;

  public ResolvedAppleItemInputBuilder WithEpisodeId(long episodeId)
  {
    _episodeId = episodeId;
    return this;
  }

  public ResolvedAppleItemInputBuilder WithTitle(string title)
  {
    _title = title;
    return this;
  }

  public ResolvedAppleItemInputBuilder WithDescription(string description)
  {
    _description = description;
    return this;
  }

  public ResolvedAppleItemInputBuilder WithRelease(DateTime release)
  {
    _release = release;
    return this;
  }

  public ResolvedAppleItemInputBuilder WithDuration(TimeSpan duration)
  {
    _duration = duration;
    return this;
  }

  public ResolvedAppleItemInputBuilder WithUrl(Uri? url)
  {
    _url = url;
    return this;
  }

  public ResolvedAppleItemInputBuilder WithImage(Uri? image)
  {
    _image = image;
    return this;
  }

  public ResolvedAppleItemInput Create()
  {
    var id = _episodeId ?? DomainTestFixture.CreateAppleIdSpecimen(_fixture);
    return new ResolvedAppleItemInput(
      id,
      _title ?? _fixture.Create<string>(),
      _description ?? _fixture.Create<string>(),
      _release ?? DomainTestFixture.CreateCatalogueReleaseSpecimen(_fixture),
      _duration ?? DomainTestFixture.CreateDurationSpecimen(_fixture),
      _url ?? new Uri($"https://podcasts.apple.com/us/podcast/episode/id{id}"),
      _image ?? new Uri($"https://example.com/apple-art-{id}.jpg"));
  }
}

/// <summary>Fluent builder for ResolvedYouTubeItem input specimens.</summary>
public sealed class ResolvedYouTubeItemInputBuilder
{
  private readonly Fixture _fixture;
  private string? _episodeId;
  private string? _title;
  private string? _description;
  private DateTime? _release;
  private TimeSpan? _duration;
  private Uri? _url;
  private Uri? _image;

  internal ResolvedYouTubeItemInputBuilder(Fixture fixture) => _fixture = fixture;

  public ResolvedYouTubeItemInputBuilder WithEpisodeId(string episodeId)
  {
    _episodeId = episodeId;
    return this;
  }

  public ResolvedYouTubeItemInputBuilder WithTitle(string title)
  {
    _title = title;
    return this;
  }

  public ResolvedYouTubeItemInputBuilder WithDescription(string description)
  {
    _description = description;
    return this;
  }

  public ResolvedYouTubeItemInputBuilder WithRelease(DateTime release)
  {
    _release = release;
    return this;
  }

  public ResolvedYouTubeItemInputBuilder WithDuration(TimeSpan duration)
  {
    _duration = duration;
    return this;
  }

  public ResolvedYouTubeItemInputBuilder WithUrl(Uri? url)
  {
    _url = url;
    return this;
  }

  public ResolvedYouTubeItemInputBuilder WithImage(Uri? image)
  {
    _image = image;
    return this;
  }

  public ResolvedYouTubeItemInput Create()
  {
    var id = _episodeId ?? DomainTestFixture.CreateYouTubeIdSpecimen(_fixture);
    return new ResolvedYouTubeItemInput(
      id,
      _title ?? _fixture.Create<string>(),
      _description ?? _fixture.Create<string>(),
      _release ?? DomainTestFixture.CreateCatalogueReleaseSpecimen(_fixture),
      _duration ?? DomainTestFixture.CreateDurationSpecimen(_fixture),
      _url ?? new Uri($"https://www.youtube.com/watch?v={id}"),
      _image);
  }
}

/// <summary>Fluent builder for episode specimens with platform-specific shortcuts.</summary>
public sealed class EpisodeBuilder
{
  private readonly DomainTestFixture _fixture;
  private readonly Episode _episode;

  internal EpisodeBuilder(DomainTestFixture fixture)
  {
    _fixture = fixture;
    _episode = fixture.CreateEpisode();
  }

  public EpisodeBuilder WithPodcast(Podcast podcast)
  {
    _episode.PodcastId = podcast.Id;
    return this;
  }

  public EpisodeBuilder WithId(Guid id)
  {
    _episode.Id = id;
    return this;
  }

  public EpisodeBuilder WithTitle(string title)
  {
    _episode.Title = title;
    return this;
  }

  public EpisodeBuilder WithDescription(string description)
  {
    _episode.Description = description;
    return this;
  }

  public EpisodeBuilder WithRelease(DateTime release)
  {
    _episode.Release = release;
    return this;
  }

  public EpisodeBuilder WithLength(TimeSpan length)
  {
    _episode.Length = length;
    return this;
  }

  public EpisodeBuilder WithSpotify(string spotifyId, Uri? url = null)
  {
    _episode.SpotifyId = spotifyId;
    if (url is not null)
      _episode.Urls.Spotify = url;
    return this;
  }

  public EpisodeBuilder WithYouTube(string youTubeId, Uri? url = null)
  {
    _episode.YouTubeId = youTubeId;
    if (url is not null)
      _episode.Urls.YouTube = url;
    return this;
  }

  public EpisodeBuilder WithApple(long appleId, Uri? url = null)
  {
    _episode.AppleId = appleId;
    if (url is not null)
      _episode.Urls.Apple = url;
    return this;
  }

  public EpisodeBuilder WithSpotifyImage(Uri image)
  {
    _episode.Images!.Spotify = image;
    return this;
  }

  public EpisodeBuilder WithYouTubeImage(Uri image)
  {
    _episode.Images!.YouTube = image;
    return this;
  }

  public EpisodeBuilder Customize(Action<Episode> configure)
  {
    configure(_episode);
    return this;
  }

  public Episode Create() => _episode;
}
