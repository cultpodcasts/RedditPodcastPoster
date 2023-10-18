using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodePoster : IPodcastEpisodePoster
{
    private readonly IEpisodePostManager _episodePostManager;
    private readonly ILogger<PodcastEpisodePoster> _logger;

    public PodcastEpisodePoster(
        IEpisodePostManager episodePostManager,
        ILogger<PodcastEpisodePoster> logger)
    {
        _episodePostManager = episodePostManager;
        _logger = logger;
    }

    public async Task<ProcessResponse> PostPodcastEpisode(PodcastEpisode podcastEpisode)
    {
        var episodes = GetEpisodes(podcastEpisode);
        var postModel = (podcastEpisode.Podcast!, episodes).ToPostModel();

        var result = await _episodePostManager.Post(postModel);

        if (result.Success)
        {
            foreach (var episode in episodes)
            {
                episode.Posted = true;
            }
        }

        return result;
    }

    private Episode[] GetEpisodes(PodcastEpisode matchingPodcastEpisode)
    {
        var orderedBundleEpisodes = Array.Empty<Episode>();
        if (matchingPodcastEpisode.Podcast!.Bundles &&
            !string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast.TitleRegex) &&
            int.TryParse(
                new Regex(matchingPodcastEpisode.Podcast.TitleRegex).Match(matchingPodcastEpisode.Episode.Title)
                    .Result("${partnumber}"), out _))
        {
            orderedBundleEpisodes = GetOrderedBundleEpisodes(matchingPodcastEpisode).ToArray();
        }

        if (!orderedBundleEpisodes.Any())
        {
            orderedBundleEpisodes = new[] {matchingPodcastEpisode.Episode}!;
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
            int.Parse(podcastTitleRegex.Match(x.Title).Result("${partnumber}")));
        return orderedBundleEpisodes;
    }
}