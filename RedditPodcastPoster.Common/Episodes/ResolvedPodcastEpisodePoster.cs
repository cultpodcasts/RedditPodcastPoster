using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class ResolvedPodcastEpisodePoster : IResolvedPodcastEpisodePoster
{
    private readonly IEpisodePostManager _episodePostManager;
    private readonly ILogger<ResolvedPodcastEpisodePoster> _logger;
    private readonly IResolvedPodcastEpisodeAdaptor _resolvedPodcastEpisodeAdaptor;

    public ResolvedPodcastEpisodePoster(
        IEpisodePostManager episodePostManager,
        IResolvedPodcastEpisodeAdaptor resolvedPodcastEpisodeAdaptor,
        ILogger<ResolvedPodcastEpisodePoster> logger)
    {
        _episodePostManager = episodePostManager;
        _resolvedPodcastEpisodeAdaptor = resolvedPodcastEpisodeAdaptor;
        _logger = logger;
    }

    public async Task<ProcessResponse> PostResolvedPodcastEpisode(ResolvedPodcastEpisode resolvedEpisode)
    {
        ProcessResponse? result;
        if (!resolvedEpisode.Episode!.Posted)
        {
            var episodes = GetEpisodes(resolvedEpisode);
            var postModel =
                _resolvedPodcastEpisodeAdaptor.ToPostModel(resolvedEpisode.Podcast!, episodes);

            result = await _episodePostManager.Post(postModel);

            if (result.Success)
            {
                foreach (var episode in episodes)
                {
                    episode.Posted = true;
                }
            }
        }
        else
        {
            result= ProcessResponse.AlreadyPosted();
        }

        return result;
    }

    private Episode[] GetEpisodes(ResolvedPodcastEpisode matchingPodcastEpisode)
    {
        var orderedBundleEpisodes = Array.Empty<Episode>();
        if (matchingPodcastEpisode.Podcast!.Bundles &&
            !string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast.TitleRegex))
        {
            orderedBundleEpisodes = GetOrderedBundleEpisodes(matchingPodcastEpisode).ToArray();
        }

        if (!orderedBundleEpisodes.Any())
        {
            orderedBundleEpisodes = new[] {matchingPodcastEpisode.Episode}!;
        }

        return orderedBundleEpisodes;
    }

    private IOrderedEnumerable<Episode> GetOrderedBundleEpisodes(ResolvedPodcastEpisode matchingPodcastEpisode)
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