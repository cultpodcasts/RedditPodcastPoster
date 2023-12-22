using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeResolver : IAppleEpisodeResolver
{
    private const int MinFuzzyScore = 70;
    private static readonly long TimeDifferenceThreshold = TimeSpan.FromSeconds(30).Ticks;
    private static readonly long BroaderTimeDifferenceThreshold = TimeSpan.FromSeconds(90).Ticks;
    private readonly ICachedApplePodcastService _applePodcastService;
    private readonly ILogger<AppleEpisodeResolver> _logger;

    public AppleEpisodeResolver(
        ICachedApplePodcastService applePodcastService,
        ILogger<AppleEpisodeResolver> logger)
    {
        _applePodcastService = applePodcastService;
        _logger = logger;
    }

    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null)
    {
        AppleEpisode? matchingEpisode = null;
        IEnumerable<AppleEpisode>? podcastEpisodes = null;
        if (request.PodcastAppleId.HasValue)
        {
            var applePodcastId = new ApplePodcastId(request.PodcastAppleId.Value);
            podcastEpisodes = await _applePodcastService.GetEpisodes(applePodcastId, indexingContext);
        }

        if (request.EpisodeAppleId != null && podcastEpisodes != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null && podcastEpisodes != null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                var matches = podcastEpisodes.Where(x => x.Title.Trim() == request.EpisodeTitle.Trim());
                var match = matches.SingleOrDefault();
                if (match == null)
                {
                    IEnumerable<AppleEpisode> sampleList;
                    if (reducer != null)
                    {
                        sampleList = podcastEpisodes.Where(reducer);
                    }
                    else
                    {
                        sampleList = podcastEpisodes;
                    }

                    var sameLength = sampleList
                        .Where(x => Math.Abs((x.Duration - request.EpisodeLength!.Value).Ticks) <
                                    TimeDifferenceThreshold);
                    if (sameLength.Count() > 1)
                    {
                        return FuzzyMatcher.Match(request.EpisodeTitle, sameLength, x => x.Title);
                    }

                    match = sameLength.SingleOrDefault(x =>
                        FuzzyMatcher.IsMatch(request.EpisodeTitle, x, y => y.Title, MinFuzzyScore));

                    if (match == null)
                    {
                        sameLength = sampleList
                            .Where(x => Math.Abs((x.Duration - request.EpisodeLength!.Value).Ticks) <
                                        BroaderTimeDifferenceThreshold);
                        return FuzzyMatcher.Match(request.EpisodeTitle, sameLength, x => x.Title, MinFuzzyScore);
                    }
                }

                return match;
            }

            _logger.LogInformation(
                $"Podcast '{request.PodcastName}' cannot be found on Apple Podcasts.");
        }

        return matchingEpisode;
    }
}