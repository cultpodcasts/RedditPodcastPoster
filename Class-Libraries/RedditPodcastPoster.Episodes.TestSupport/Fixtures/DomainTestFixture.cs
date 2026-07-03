using AutoFixture;
using AutoFixture.Dsl;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Shared AutoFixture wrapper for episode-domain business-rule tests.
/// Owns specimen defaults, incident constants, and factory helpers.
/// </summary>
public sealed class DomainTestFixture
{
  public static readonly TimeSpan DefaultLength = TimeSpan.FromMinutes(45);
  public static readonly DateTime DefaultRelease = DateTime.UtcNow.AddMonths(-1);
  public const string DefaultEpisodeTitle = "Episode title";
  public const string DefaultCatalogueDescription = "Catalogue description";
  public const string DefaultYouTubeDescription = "YouTube description";
  public const string DefaultAppleDescription = "Apple description";

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

  public SpotifyCatalogueInput CreateSpotifyCatalogueInput(
    string? spotifyId = null,
    DateTime? release = null,
    Uri? spotifyUrl = null,
    Uri? image = null)
  {
    var id = spotifyId ?? "6O1Z1s7ca0PI8Gq1rdt3j4";
    return new SpotifyCatalogueInput(
      id,
      DefaultEpisodeTitle,
      DefaultCatalogueDescription,
      DefaultLength,
      release ?? DefaultRelease,
      spotifyUrl ?? DefaultSpotifyUrl(id),
      image ?? DefaultSpotifyImage(id));
  }

  public AppleCatalogueInput CreateAppleCatalogueInput(
    long? appleId = null,
    DateTime? release = null,
    Uri? appleUrl = null,
    Uri? image = null)
  {
    var id = appleId ?? 1234567890L;
    return new AppleCatalogueInput(
      id,
      DefaultEpisodeTitle,
      DefaultAppleDescription,
      DefaultLength,
      release ?? DefaultRelease,
      appleUrl ?? DefaultAppleUrl(id),
      image);
  }

  public YouTubeCatalogueInput CreateYouTubeCatalogueInput(
    string? youTubeId = null,
    DateTime? release = null,
    Uri? youTubeUrl = null,
    Uri? image = null)
  {
    var id = youTubeId ?? "dQw4w9WgXcQ";
    return new YouTubeCatalogueInput(
      id,
      DefaultEpisodeTitle,
      DefaultYouTubeDescription,
      DefaultLength,
      release ?? DefaultRelease,
      youTubeUrl ?? DefaultYouTubeUrl(id),
      image ?? DefaultYouTubeImage(id));
  }

  public ResolvedSpotifyItemInput CreateResolvedSpotifyItemInput(
    string? episodeId = null,
    DateTime? release = null,
    Uri? url = null,
    Uri? image = null)
  {
    var id = episodeId ?? "submit-spot-1";
    return new ResolvedSpotifyItemInput(
      id,
      DefaultEpisodeTitle,
      DefaultCatalogueDescription,
      release ?? DefaultRelease,
      DefaultLength,
      url ?? DefaultSpotifyUrl(id),
      image ?? DefaultSpotifyImage(id));
  }

  public ResolvedAppleItemInput CreateResolvedAppleItemInput(
    long? episodeId = null,
    DateTime? release = null,
    Uri? url = null,
    Uri? image = null)
  {
    var id = episodeId ?? 1112223334L;
    return new ResolvedAppleItemInput(
      id,
      DefaultEpisodeTitle,
      DefaultAppleDescription,
      release ?? DefaultRelease,
      DefaultLength,
      url ?? DefaultAppleUrl(id),
      image ?? DefaultAppleImage(id));
  }

  public ResolvedYouTubeItemInput CreateResolvedYouTubeItemInput(
    string? episodeId = null,
    DateTime? release = null,
    Uri? url = null,
    Uri? image = null)
  {
    var id = episodeId ?? "yt-only-submit";
    return new ResolvedYouTubeItemInput(
      id,
      DefaultEpisodeTitle,
      DefaultYouTubeDescription,
      release ?? DefaultRelease,
      DefaultLength,
      url ?? DefaultYouTubeUrl(id),
      image ?? DefaultYouTubeImage(id));
  }

  public IFixture Auto => _fixture;

  public IPostprocessComposer<T> Build<T>() => _fixture.Build<T>();

  public T Create<T>() => _fixture.Create<T>();

  public Podcast CreatePodcast(Action<Podcast>? customize = null)
  {
    var podcast = new Podcast
    {
      Id = Guid.NewGuid(),
      Name = "Test Podcast",
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
      Title = DefaultEpisodeTitle,
      Description = string.Empty,
      Release = DefaultRelease,
      Length = DefaultLength,
      Urls = new ServiceUrls(),
      Images = new EpisodeImages(),
      Subjects = [],
      SpotifyId = string.Empty,
      YouTubeId = string.Empty
    };
    customize?.Invoke(episode);
    return episode;
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
    Uri? image = null) =>
    Episode.FromSpotify(
      spotifyId,
      title ?? DefaultEpisodeTitle,
      description ?? DefaultCatalogueDescription,
      length ?? DefaultLength,
      false,
      release ?? DefaultRelease,
      spotifyUrl ?? DefaultSpotifyUrl(spotifyId),
      image);

  public Episode CreateYouTubeCatalogueEpisode(
    string youTubeId,
    string? title = null,
    DateTime? release = null,
    TimeSpan? length = null,
    string? description = null,
    Uri? youTubeUrl = null,
    Uri? image = null) =>
    Episode.FromYouTube(
      youTubeId,
      title ?? DefaultEpisodeTitle,
      description ?? DefaultYouTubeDescription,
      length ?? DefaultLength,
      false,
      release ?? DefaultRelease,
      youTubeUrl ?? DefaultYouTubeUrl(youTubeId),
      image);

  public Episode CreateAppleCatalogueEpisode(
    long appleId,
    string? title = null,
    DateTime? release = null,
    TimeSpan? length = null,
    string? description = null,
    Uri? appleUrl = null) =>
    Episode.FromApple(
      appleId,
      title ?? DefaultEpisodeTitle,
      description ?? DefaultAppleDescription,
      length ?? DefaultLength,
      false,
      release ?? DefaultRelease,
      appleUrl ?? DefaultAppleUrl(appleId),
      null);

  private void CustomizeFixture()
  {
    _fixture.Register(() => new Uri("https://example.com/test"));
    _fixture.Customize<Podcast>(composer => composer.FromFactory(() => CreatePodcast()));
    _fixture.Customize<Episode>(composer => composer.FromFactory(() => CreateEpisode()));
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
