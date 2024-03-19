using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodePoster(
    IEpisodePostManager episodePostManager,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastEpisodePoster> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastEpisodePoster
{
    private readonly ILogger<PodcastEpisodePoster> _logger = logger;

    public async Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisode podcastEpisode,
        bool preferYouTube = false)
    {
        try
        {
            var episodes = GetEpisodes(podcastEpisode);

            var postModel = (podcastEpisode.Podcast!, episodes).ToPostModel(preferYouTube);

            var result = await episodePostManager.Post(postModel);

            if (result.Success)
            {
                foreach (var episode in episodes)
                {
                    episode.Posted = true;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            var message =
                $"Failure to post episode '{podcastEpisode.Episode.Title}' and episode-id '{podcastEpisode.Episode.Id}' for podcast '{podcastEpisode.Podcast.Name}' with podcast-id '{podcastEpisode.Podcast.Id}'";
            _logger.LogError(ex, $"{message}.");
            return ProcessResponse.Fail($"{message} - Exception: '{ex.Message}'.");
        }
    }

    private Episode[] GetEpisodes(PodcastEpisode matchingPodcastEpisode)
    {
        var orderedBundleEpisodes = Array.Empty<Episode>();
        var titleRegex = new Regex(matchingPodcastEpisode.Podcast.TitleRegex);
        var titleMatch = titleRegex.Match(matchingPodcastEpisode.Episode.Title);
        if (titleMatch.Success)
        {
            var partNumber = titleMatch.Result("${partnumber}");
            if (matchingPodcastEpisode.Podcast!.Bundles &&
                !string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast.TitleRegex) &&
                int.TryParse(partNumber, out _))
            {
                orderedBundleEpisodes = GetOrderedBundleEpisodes(matchingPodcastEpisode).ToArray();
            }
        }

        if (!orderedBundleEpisodes.Any())
        {
            orderedBundleEpisodes = new[] {matchingPodcastEpisode.Episode};
        }

        return orderedBundleEpisodes;
    }

    private IOrderedEnumerable<Episode> GetOrderedBundleEpisodes(PodcastEpisode matchingPodcastEpisode)
    {
        if (string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast!.TitleRegex))
        {
            throw new InvalidOperationException(
                $"Podcast with bundles must provide a {nameof(matchingPodcastEpisode.Podcast.TitleRegex)}. Podcast in error: id='{matchingPodcastEpisode.Podcast.Id}', name='{matchingPodcastEpisode.Podcast.Name}'. Cannot bundle episodes without a Title-Regex to collate bundles");
        }

        var podcastTitleRegex = new Regex(matchingPodcastEpisode.Podcast.TitleRegex);
        var rawTitle = podcastTitleRegex.Match(matchingPodcastEpisode.Episode!.Title).Result("${title}");
        var bundleEpisodes = matchingPodcastEpisode.Podcast.Episodes.Where(x => x.Title.Contains(rawTitle));
        var orderedBundleEpisodes = bundleEpisodes.OrderBy(x =>
            {
                var match = podcastTitleRegex.Match(x.Title);
                var partNumber = match.Result("${partnumber}");
                return int.Parse(partNumber);
            }
            );
        return orderedBundleEpisodes;
    }
}