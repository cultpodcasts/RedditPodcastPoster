using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleEpisodeResolver : IAppleEpisodeResolver
{
    private readonly ICachedApplePodcastService _applePodcastService;
    private readonly ILogger<AppleEpisodeResolver> _logger;

    public AppleEpisodeResolver(
        ICachedApplePodcastService applePodcastService,
        ILogger<AppleEpisodeResolver> logger)
    {
        _applePodcastService = applePodcastService;
        _logger = logger;
    }

    public async Task<AppleEpisode?> FindEpisode(FindAppleEpisodeRequest request)
    {
        AppleEpisode? matchingEpisode = null;
        IEnumerable<AppleEpisode> podcastEpisodes = new List<AppleEpisode>();
        if (request.PodcastAppleId.HasValue)
        {
            podcastEpisodes = await _applePodcastService.GetEpisodes(request.PodcastAppleId.Value, request.Released.Date);
        }

        if (request.EpisodeAppleId != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                var matchingEpisodes = podcastEpisodes.Where(x => x.Title == request.EpisodeTitle);
                if (!matchingEpisodes.Any() || matchingEpisodes.Count() > 1)
                {
                    var sameDateMatches = podcastEpisodes.Where(x =>
                        DateOnly.FromDateTime(x.Release) == DateOnly.FromDateTime(request.Released));
                    if (sameDateMatches.Count() > 1)
                    {
                        var distances =
                            sameDateMatches.OrderByDescending(x =>
                                Levenshtein.CalculateSimilarity(request.EpisodeTitle, x.Title));
                        return distances.FirstOrDefault()!;
                    }

                    matchingEpisode = sameDateMatches.SingleOrDefault();
                }

                matchingEpisode ??= matchingEpisodes.FirstOrDefault();

                if (matchingEpisode == null)
                {
                    _logger.LogInformation($"{nameof(FindEpisode)} Did not find matching episode for '{request.EpisodeTitle}' on podcast '{request.PodcastName}' and release-date '{request.Released:R}' from Apple.");
                }
                else
                {
                    _logger.LogInformation($"{nameof(FindEpisode)} Found matching episode for '{request.EpisodeTitle}' on podcast '{request.PodcastName}' and release-date '{request.Released:R}' from Apple with title '{matchingEpisode.Title}' and release-date '{matchingEpisode.Release:R}'.");
                }
            }
            else
            {
                _logger.LogInformation(
                    $"Podcast '{request.PodcastName}' cannot be found on Apple Podcasts.");
            }
        }

        return matchingEpisode;
    }
}