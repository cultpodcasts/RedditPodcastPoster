using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleEpisodeResolver : IAppleEpisodeResolver
{
    private const int PodcastSearchLimit = 200;
    private readonly IApplePodcastService _applePodcastService;
    private readonly ILogger<AppleEpisodeResolver> _logger;

    public AppleEpisodeResolver(
        IApplePodcastService applePodcastService,
        ILogger<AppleEpisodeResolver> logger)
    {
        _applePodcastService = applePodcastService;
        _logger = logger;
    }

    public async Task<AppleEpisode?> FindEpisode(FindAppleEpisodeRequest request)
    {
        AppleEpisode? matchingEpisode = null;
        var podcastEpisodes = await _applePodcastService.GetEpisodes(request.PodcastAppleId.Value);
        if (request.EpisodeAppleId != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                if (request.EpisodeIndex <= PodcastSearchLimit)
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
                }
                else
                {
                    _logger.LogInformation(
                        $"Podcast '{request.PodcastName}' episode with title '{request.EpisodeTitle}' and release-date '{request.Released}' is beyond limit of Apple Lookup.");
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