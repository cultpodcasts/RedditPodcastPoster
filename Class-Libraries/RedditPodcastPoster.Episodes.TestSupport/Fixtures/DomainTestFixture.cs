using AutoFixture;
using AutoFixture.Dsl;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Shared AutoFixture wrapper for episode-domain business-rule tests.
/// Owns specimen customizations and scenario-shaped factory helpers.
/// <para>
/// <b>Specimen contract</b> — default catalogue/resolved-item builders produce platform-realistic
/// IDs, URLs, and releases. Tests trust these defaults unless a rule asserts an exact release or a
/// relationship between two releases (same-calendar-date backfill, cross-platform offset, delayed publishing).
/// </para>
/// <list type="table">
/// <listheader><term>Entity</term><description>Release / ID / URL</description></listheader>
/// <item><term>Spotify catalogue / resolved</term>
/// <description>Date-only midnight UTC (<see cref="UtcDateDaysAgo"/>); 22-char base62 id;
/// <c>open.spotify.com/episode/{id}</c></description></item>
/// <item><term>Apple catalogue / resolved</term>
/// <description>Full UTC datetime with non-midnight time-of-day (fixture-generated day offset and
/// publish time); long numeric id ≥13 digits; <c>podcasts.apple.com/.../id{id}</c></description></item>
/// <item><term>YouTube catalogue / resolved</term>
/// <description>Full UTC datetime with non-midnight time-of-day (fixture-generated day offset and
/// publish time); 11-char id; <c>youtube.com/watch?v={id}</c></description></item>
/// </list>
/// Use <see cref="CreateMidnightUtcStoredEpisode"/> plus
/// <see cref="CreateYouTubeCatalogueEpisodeSameDayAs"/> / <see cref="CreateAppleCatalogueEpisodeSameDayAs"/>
/// for same-calendar-date merge probes.
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

  /// <summary>Combines a date-only stored release with an explicit UTC time-of-day.</summary>
  public static DateTime SameCalendarDateWithTime(DateTime dateOnlyRelease, TimeSpan timeOfDay) =>
    dateOnlyRelease.Date.Add(timeOfDay);

  /// <summary>Default negative publishing delay for YouTube-first podcasts (~33½ days).</summary>
  public static long DefaultNegativeYouTubePublishingDelayTicks =>
    TimeSpan.FromDays(-33).Add(TimeSpan.FromHours(-12)).Ticks;

  /// <summary>Spotify catalogue release N calendar days after a YouTube publish date.</summary>
  public static DateTime SpotifyCatalogueReleaseDaysAfterYouTube(
    DateTime youTubeRelease,
    int calendarDaysAfter) =>
    youTubeRelease.Date.AddDays(calendarDaysAfter);

  /// <summary>Catalogue title variant that fuzzy-matches the stored title.</summary>
  public static string CreateFuzzyTitleVariant(string title) =>
    title.Length >= 3 ? title[..^1] + "…" : title + "…";

  /// <summary>Single-character typo variant for fuzzy title matching probes.</summary>
  public static string CreateTypoTitleVariant(string title)
  {
    if (title.Length < 4)
      return title + "x";
    var i = title.Length / 2;
    var replacement = title[i] == 'a' ? 'e' : 'a';
    return title[..i] + replacement + title[(i + 1)..];
  }

  private readonly Fixture _fixture;

  public DomainTestFixture()
  {
    _fixture = new Fixture();
    CustomizeFixture();
  }

  /// <summary>Production-like Spotify episode ID for generic tests (22-char base62).</summary>
  public string CreateSpotifyId() => CreateSpotifyIdSpecimen(_fixture);

  /// <summary>Production-like Apple episode ID for generic tests (≥13 digits).</summary>
  public long CreateAppleId() => CreateAppleIdSpecimen(_fixture);

  /// <summary>Production-like YouTube video ID for generic tests (11 chars).</summary>
  public string CreateYouTubeId() => CreateYouTubeIdSpecimen(_fixture);

  /// <summary>Realistic episode duration (1–120 minutes).</summary>
  public TimeSpan CreateDuration() => CreateDurationSpecimen(_fixture);

  /// <summary>UTC time-of-day with non-midnight seconds (Apple/YouTube publish times).</summary>
  public TimeSpan CreateNonMidnightTimeOfDay() => CreateNonMidnightTimeOfDaySpecimen(_fixture);

  /// <summary>YouTube channel ID (UC prefix + 22 chars).</summary>
  public string CreateYouTubeChannelId() =>
    "UC" + CreateRandomString(_fixture, YouTubeIdAlphabet, 22);

  /// <summary>Random Guid for generic test identity (podcast/episode ids not asserted).</summary>
  public Guid CreateGuid() => _fixture.Create<Guid>();

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
      if (id.HasValue)
        p.Id = id.Value;
      p.Name = "YouTube-first podcast";
      p.ReleaseAuthority = Service.YouTube;
      p.YouTubeChannelId = channelId;
      p.YouTubePublicationOffset = youTubePublicationOffsetTicks;
      p.SpotifyId = spotifyShowId ?? string.Empty;
    });

  public Podcast CreateYouTubeFirstPodcastWithNegativeDelay(
    long? youTubePublicationOffsetTicks = null,
    string? spotifyShowId = null) =>
    CreateYouTubeFirstPodcast(
      CreateYouTubeChannelId(),
      youTubePublicationOffsetTicks ?? DefaultNegativeYouTubePublishingDelayTicks,
      spotifyShowId ?? CreateSpotifyId());

  public Episode CreateEpisode(Action<Episode>? customize = null)
  {
    var episode = new Episode
    {
      Id = Guid.NewGuid(),
      PodcastId = Guid.NewGuid(),
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

  /// <summary>Stored episode with YouTube identity only (no Spotify/Apple).</summary>
  public Episode CreateStoredEpisodeWithYouTubeOnly(
    Podcast podcast,
    DateTime? release = null,
    TimeSpan? length = null,
    string? title = null)
  {
    var youTubeId = CreateYouTubeId();
    return CreateStoredEpisode(podcast, e =>
    {
      if (title is not null)
        e.Title = title;
      e.Release = release ?? UtcAtTime(-30, CreateNonMidnightTimeOfDay());
      e.Length = length ?? CreateDuration();
      e.YouTubeId = youTubeId;
      e.Urls.YouTube = DefaultYouTubeUrl(youTubeId);
    });
  }

  /// <summary>Stored episode with Spotify identity only (no YouTube/Apple).</summary>
  public Episode CreateStoredEpisodeWithSpotifyOnly(
    Podcast podcast,
    DateTime? release = null,
    TimeSpan? length = null,
    string? title = null)
  {
    var spotifyId = CreateSpotifyId();
    return CreateStoredEpisode(podcast, e =>
    {
      if (title is not null)
        e.Title = title;
      e.Release = release ?? UtcAtTime(-2, CreateNonMidnightTimeOfDay());
      e.Length = length ?? CreateDuration();
      e.SpotifyId = spotifyId;
      e.Urls.Spotify = DefaultSpotifyUrl(spotifyId);
    });
  }

  /// <summary>Stored episode with both YouTube and Spotify identities.</summary>
  public Episode CreateStoredEpisodeWithYouTubeAndSpotify(
    Podcast podcast,
    string? spotifyId = null,
    string? youTubeId = null,
    DateTime? release = null,
    TimeSpan? length = null,
    string? title = null)
  {
    var sid = spotifyId ?? CreateSpotifyId();
    var yid = youTubeId ?? CreateYouTubeId();
    return CreateStoredEpisode(podcast, e =>
    {
      if (title is not null)
        e.Title = title;
      e.Release = release ?? UtcAtTime(-30, CreateNonMidnightTimeOfDay());
      e.Length = length ?? CreateDuration();
      e.YouTubeId = yid;
      e.SpotifyId = sid;
      e.Urls.YouTube = DefaultYouTubeUrl(yid);
      e.Urls.Spotify = DefaultSpotifyUrl(sid);
    });
  }

  /// <summary>
  /// YouTube-only stored row plus Spotify catalogue incoming shaped for YouTube-first cross-platform matching.
  /// </summary>
  public (Episode Stored, Episode Incoming, string SpotifyId) CreateCrossPlatformYouTubeFirstPair(
    Podcast podcast,
    int youTubeReleaseDaysAgo = 30,
    int spotifyDaysAfterYouTube = 28,
    bool fuzzyTitleVariant = true)
  {
    var youTubeRelease = UtcAtTime(-youTubeReleaseDaysAgo, CreateNonMidnightTimeOfDay());
    var spotifyRelease = SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, spotifyDaysAfterYouTube);
    var storedLength = CreateDuration();
    var incomingLength = storedLength + TimeSpan.FromMinutes(3);
    var storedTitle = Create<string>();
    var incomingTitle = fuzzyTitleVariant ? CreateFuzzyTitleVariant(storedTitle) : storedTitle;

    var stored = CreateStoredEpisodeWithYouTubeOnly(
      podcast,
      youTubeRelease,
      storedLength,
      storedTitle);
    var spotifyInput = CreateSpotifyCatalogueInput(b => b
      .WithTitle(incomingTitle)
      .WithRelease(spotifyRelease)
      .WithDuration(incomingLength));
    var incoming = CreateSpotifyCatalogueEpisode(b => b
      .WithSpotifyId(spotifyInput.SpotifyId)
      .WithTitle(incomingTitle)
      .WithSpotifyUrl(spotifyInput.SpotifyUrl)
      .WithRelease(spotifyRelease)
      .WithDuration(incomingLength));

    return (stored, incoming, spotifyInput.SpotifyId);
  }

  /// <summary>
  /// Stored YouTube row and Spotify incoming with aligned release/duration but clearly different titles —
  /// negative-delay guard must not merge on release alone.
  /// </summary>
  public (Episode Stored, Episode Incoming) CreateNegativeDelayNonMatchingPair(Podcast podcast)
  {
    const int storedDaysAgo = 34;
    const int incomingDaysAfterStored = 28;
    var storedRelease = UtcAtTime(-storedDaysAgo, CreateNonMidnightTimeOfDay());
    var incomingRelease = UtcDateDaysAgo(storedDaysAgo - incomingDaysAfterStored);
    var storedLength = CreateDuration();
    var incomingLength = storedLength - TimeSpan.FromSeconds(5);

    var stored = CreateStoredEpisodeWithYouTubeOnly(
      podcast,
      storedRelease,
      storedLength,
      Create<string>());
    var incoming = CreateSpotifyCatalogueEpisode(b => b
      .WithTitle(Create<string>())
      .WithRelease(incomingRelease)
      .WithDuration(incomingLength));

    return (stored, incoming);
  }

  public SpotifyCatalogueInput CreateSpotifyCatalogueInputDaysAfterYouTubeRelease(
    DateTime youTubeRelease,
    int calendarDaysAfter,
    Action<SpotifyCatalogueInputBuilder>? configure = null)
  {
    var release = SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, calendarDaysAfter);
    return CreateSpotifyCatalogueInput(b =>
    {
      b.WithRelease(release);
      configure?.Invoke(b);
    });
  }

  public (Episode YouTubeOnly, Episode AppleOnly) CreateAmbiguousMatchStoredEpisodes(
    Podcast podcast,
    DateTime release,
    TimeSpan length,
    string? sharedTitle = null)
  {
    var title = sharedTitle ?? Create<string>();
    var youTubeId = CreateYouTubeId();
    var appleId = CreateAppleId();
    var youTubeOnly = CreateStoredEpisode(podcast, e =>
    {
      e.Title = title;
      e.Release = release;
      e.Length = length;
      e.YouTubeId = youTubeId;
      e.Urls.YouTube = DefaultYouTubeUrl(youTubeId);
    });
    var appleOnly = CreateStoredEpisode(podcast, e =>
    {
      e.Title = title;
      e.Release = release;
      e.Length = length;
      e.AppleId = appleId;
      e.Urls.Apple = DefaultAppleUrl(appleId);
    });
    return (youTubeOnly, appleOnly);
  }

  public Episode CreateAmbiguousMatchSpotifyIncoming(
    DateTime release,
    TimeSpan length,
    string? sharedTitle = null)
  {
    var title = sharedTitle ?? Create<string>();
    var spotifyId = CreateSpotifyId();
    return CreateSpotifyCatalogueEpisode(
      spotifyId,
      title,
      DefaultSpotifyUrl(spotifyId),
      release,
      length);
  }

  public Episode CreatePositiveDelayAudioStoredEpisode(
    Podcast podcast,
    DateTime? audioRelease = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Release = audioRelease ?? UtcDaysAgo(_fixture.Create<int>() % 365 + 1);
      e.Length = length ?? CreateDuration();
      var spotifyId = CreateSpotifyId();
      e.SpotifyId = spotifyId;
      e.Urls.Spotify = DefaultSpotifyUrl(spotifyId);
    });

  public Episode CreateMidnightUtcSpotifyStoredEpisode(
    Podcast podcast,
    DateTime dateOnlyRelease,
    string? title = null,
    TimeSpan? length = null) =>
    CreateMidnightUtcStoredEpisode(podcast, dateOnlyRelease, title, length);

  /// <summary>Stored row with midnight UTC release (Spotify id/url for merge probes).</summary>
  public Episode CreateMidnightUtcStoredEpisode(
    Podcast podcast,
    DateTime dateOnlyRelease,
    string? title = null,
    TimeSpan? length = null) =>
    CreateStoredEpisode(podcast, e =>
    {
      e.Release = dateOnlyRelease;
      var spotifyId = CreateSpotifyId();
      e.SpotifyId = spotifyId;
      e.Urls.Spotify = DefaultSpotifyUrl(spotifyId);
      if (title is not null)
        e.Title = title;
      if (length is not null)
        e.Length = length.Value;
    });

  public YouTubeCatalogueInput CreateYouTubeCatalogueInputSameDayAs(
    Episode stored,
    TimeSpan? timeOfDay = null,
    Action<YouTubeCatalogueInputBuilder>? configure = null)
  {
    var release = SameCalendarDateWithTime(
      stored.Release,
      timeOfDay ?? CreateNonMidnightTimeOfDaySpecimen(_fixture));
    return CreateYouTubeCatalogueInput(b =>
    {
      b.WithRelease(release);
      configure?.Invoke(b);
    });
  }

  public Episode CreateYouTubeCatalogueEpisodeSameDayAs(
    Episode stored,
    TimeSpan? timeOfDay = null,
    Action<YouTubeCatalogueInputBuilder>? configure = null)
  {
    var input = CreateYouTubeCatalogueInputSameDayAs(stored, timeOfDay, configure);
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

  public AppleCatalogueInput CreateAppleCatalogueInputSameDayAs(
    Episode stored,
    TimeSpan? timeOfDay = null,
    Action<AppleCatalogueInputBuilder>? configure = null)
  {
    var release = SameCalendarDateWithTime(
      stored.Release,
      timeOfDay ?? CreateNonMidnightTimeOfDaySpecimen(_fixture));
    return CreateAppleCatalogueInput(b =>
    {
      b.WithRelease(release);
      configure?.Invoke(b);
    });
  }

  public Episode CreateAppleCatalogueEpisodeSameDayAs(
    Episode stored,
    TimeSpan? timeOfDay = null,
    Action<AppleCatalogueInputBuilder>? configure = null)
  {
    var input = CreateAppleCatalogueInputSameDayAs(stored, timeOfDay, configure);
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
      e.PodcastId = podcastId ?? Guid.NewGuid();
      e.Title = title;
      e.Release = release ?? DateTime.UtcNow.Date;
      e.Urls.Spotify = spotifyUrl;
    });

  public Episode CreateSpotifyCatalogueEpisode(
    Action<SpotifyCatalogueInputBuilder>? configure = null)
  {
    var input = CreateSpotifyCatalogueInput(configure);
    return Episode.FromSpotify(
      input.SpotifyId,
      input.Title,
      input.Description,
      input.Duration,
      false,
      input.Release,
      input.SpotifyUrl,
      input.Image);
  }

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
    Action<YouTubeCatalogueInputBuilder>? configure = null)
  {
    var input = CreateYouTubeCatalogueInput(configure);
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
    Action<AppleCatalogueInputBuilder>? configure = null)
  {
    var input = CreateAppleCatalogueInput(configure);
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
    _fixture.Customize<SpotifyCatalogueInput>(composer => composer.FromFactory(
      () => BuildSpotifyCatalogueInput().Create()));
    _fixture.Customize<AppleCatalogueInput>(composer => composer.FromFactory(
      () => BuildAppleCatalogueInput().Create()));
    _fixture.Customize<YouTubeCatalogueInput>(composer => composer.FromFactory(
      () => BuildYouTubeCatalogueInput().Create()));
    _fixture.Customize<ResolvedSpotifyItemInput>(composer => composer.FromFactory(
      () => BuildResolvedSpotifyItemInput().Create()));
    _fixture.Customize<ResolvedAppleItemInput>(composer => composer.FromFactory(
      () => BuildResolvedAppleItemInput().Create()));
    _fixture.Customize<ResolvedYouTubeItemInput>(composer => composer.FromFactory(
      () => BuildResolvedYouTubeItemInput().Create()));
  }

  private const string Base62 =
    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

  private const string YouTubeIdAlphabet =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

  internal static string CreateSpotifyIdSpecimen(Fixture fixture) =>
    CreateRandomString(fixture, Base62, 22);

  internal static string CreateSpotifyShowIdSpecimen(Fixture fixture) =>
    CreateRandomString(fixture, Base62, 22);

  internal static long CreateAppleIdSpecimen(Fixture fixture) =>
    Math.Abs(fixture.Create<long>()) % 9_000_000_000_000L + 1_000_000_000_000L;

  internal static string CreateYouTubeIdSpecimen(Fixture fixture) =>
    CreateRandomString(fixture, YouTubeIdAlphabet, 11);

  internal static TimeSpan CreateDurationSpecimen(Fixture fixture) =>
    TimeSpan.FromMinutes(fixture.Create<int>() % 120 + 1);

  internal static DateTime CreateSpotifyReleaseSpecimen(Fixture fixture) =>
    UtcDateDaysAgo(fixture.Create<int>() % 365 + 1);

  internal static DateTime CreateAppleReleaseSpecimen(Fixture fixture) =>
    UtcAtTime(-(fixture.Create<int>() % 365 + 1), CreateNonMidnightTimeOfDaySpecimen(fixture));

  internal static DateTime CreateYouTubeReleaseSpecimen(Fixture fixture) =>
    UtcAtTime(-(fixture.Create<int>() % 365 + 1), CreateNonMidnightTimeOfDaySpecimen(fixture));

  internal static TimeSpan CreateNonMidnightTimeOfDaySpecimen(Fixture fixture)
  {
    var seconds = Math.Abs(fixture.Create<int>()) % (int)TimeSpan.FromDays(1).TotalSeconds;
    if (seconds == 0)
      seconds = 1;
    return TimeSpan.FromSeconds(seconds);
  }

  private static string CreateRandomString(Fixture fixture, string alphabet, int length) =>
    new string(Enumerable.Range(0, length)
      .Select(_ => alphabet[Math.Abs(fixture.Create<int>()) % alphabet.Length])
      .ToArray());
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
      _release ?? DomainTestFixture.CreateAppleReleaseSpecimen(_fixture),
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
      _release ?? DomainTestFixture.CreateYouTubeReleaseSpecimen(_fixture),
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
      _release ?? DomainTestFixture.CreateAppleReleaseSpecimen(_fixture),
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
      _release ?? DomainTestFixture.CreateYouTubeReleaseSpecimen(_fixture),
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
