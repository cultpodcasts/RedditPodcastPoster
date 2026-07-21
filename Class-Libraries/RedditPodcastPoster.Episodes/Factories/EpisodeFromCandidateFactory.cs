using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Factories;

public sealed class EpisodeFromCandidateFactory : IEpisodeFromCandidateFactory
{
    public Episode Create(EpisodeCandidate candidate, bool explicitContent)
    {
        var episode = new Episode
        {
            Title = candidate.Title,
            Description = candidate.Description,
            Length = candidate.Duration,
            Explicit = explicitContent,
            Release = candidate.Release.Value
        };

        if (candidate.SourceLink is not { } link)
        {
            return episode;
        }

        switch (link.Service)
        {
            case Service.Spotify:
                episode.SpotifyId = link.Id ?? string.Empty;
                episode.Urls = new ServiceUrls { Spotify = link.Url };
                if (link.Image != null)
                {
                    episode.Images = new EpisodeImages { Spotify = link.Image };
                }

                break;
            case Service.Apple:
                if (link.Id != null && long.TryParse(link.Id, out var appleId))
                {
                    episode.AppleId = appleId;
                }

                episode.Urls = new ServiceUrls { Apple = link.Url };
                if (link.Image != null)
                {
                    episode.Images = new EpisodeImages { Apple = link.Image };
                }

                break;
            case Service.YouTube:
                episode.YouTubeId = link.Id ?? string.Empty;
                episode.Urls = new ServiceUrls { YouTube = link.Url };
                if (link.Image != null)
                {
                    episode.Images = new EpisodeImages { YouTube = link.Image };
                }

                break;
        }

        return episode;
    }
}
