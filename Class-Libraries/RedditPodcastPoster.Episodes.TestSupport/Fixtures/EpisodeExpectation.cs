using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

public sealed record PlatformExpectation(string? Id, Uri? Url, Uri? Image = null)
{
    public static PlatformExpectation? FromCatalog(Episode episode, Service service)
    {
        var (id, url, image) = service switch
        {
            Service.Spotify => (
                EpisodeServicePresence.SpotifyEpisodeId(episode),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Spotify)),
            Service.Apple => (
                EpisodeServicePresence.AppleEpisodeId(episode)?.ToString(),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Apple)),
            Service.YouTube => (
                EpisodeServicePresence.YouTubeEpisodeId(episode),
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
                EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube)),
            _ => (null, null, null)
        };

        if (string.IsNullOrWhiteSpace(id) && url is null && image is null)
        {
            return null;
        }

        return new PlatformExpectation(
            string.IsNullOrWhiteSpace(id) ? null : id,
            url,
            image);
    }
}

public sealed record EpisodeExpectation(
    PlatformExpectation? Spotify,
    PlatformExpectation? Apple,
    PlatformExpectation? YouTube,
    DateTime Release,
    string Description,
    bool Ignored = false,
    bool Removed = false)
{
    public static EpisodeExpectation From(Episode episode) =>
        new(
            PlatformExpectation.FromCatalog(episode, Service.Spotify),
            PlatformExpectation.FromCatalog(episode, Service.Apple),
            PlatformExpectation.FromCatalog(episode, Service.YouTube),
            episode.Release,
            episode.Description,
            episode.Ignored,
            episode.Removed);

    public static EpisodeExpectation From(EpisodeCandidate candidate)
    {
        PlatformExpectation? spotify = null;
        PlatformExpectation? apple = null;
        PlatformExpectation? youtube = null;

        if (candidate.SourceLink is { } link)
        {
            var platform = new PlatformExpectation(link.Id, link.Url, link.Image);
            switch (link.Service)
            {
                case Service.Spotify:
                    spotify = platform;
                    break;
                case Service.Apple:
                    apple = platform;
                    break;
                case Service.YouTube:
                    youtube = platform;
                    break;
            }
        }

        return new EpisodeExpectation(
            spotify,
            apple,
            youtube,
            candidate.Release.Value,
            candidate.Description);
    }

    public EpisodeExpectation WithSpotify(string id, Uri url, Uri? image = null) =>
        this with { Spotify = new PlatformExpectation(id, url, image) };

    public EpisodeExpectation WithYouTube(string id, Uri url, Uri? image = null) =>
        this with { YouTube = new PlatformExpectation(id, url, image) };

    public EpisodeExpectation WithApple(long id, Uri url, Uri? image = null) =>
        this with { Apple = new PlatformExpectation(id.ToString(), url, image) };

    public EpisodeExpectation WithRelease(DateTime release) =>
        this with { Release = release };

    public EpisodeExpectation WithDescription(string description) =>
        this with { Description = description };
}
