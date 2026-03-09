using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Factories;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodePoster(
    IEpisodePostManager episodePostManager,
    IPostModelFactory postModelFactory,
    IEpisodeRepository episodeRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastEpisodePoster> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastEpisodePoster
{
    private static readonly TimeSpan BundledEpisodeReleaseThreshold = TimeSpan.FromDays(7);

    public async Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisodeV2 podcastEpisode,
        bool preferYouTube = false)
    {
        try
        {
            var episodes = await GetEpisodes(podcastEpisode);
            var postModel = postModelFactory.ToPostModel((podcastEpisode.Podcast, episodes), preferYouTube);
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
                $"Failure to post episode '{podcastEpisode.Episode.Title}' and episode-id '{podcastEpisode.Episode.Id}' for podcast '{podcastEpisode.Podcast.Name}' with podcast-id '{podcastEpisode.Podcast.Id}'.";
            logger.LogError(ex, message);
            return ProcessResponse.Fail($"{message} - Exception: '{ex.Message}'.");
        }
    }

    private async Task<Episode[]> GetEpisodes(PodcastEpisodeV2 matchingPodcastEpisode)
    {
        var orderedBundleEpisodes = Array.Empty<Episode>();

        if (matchingPodcastEpisode.Podcast.Bundles &&
            !string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast.TitleRegex))
        {
            var titleRegex = new Regex(matchingPodcastEpisode.Podcast.TitleRegex, Podcast.TitleFlags);
            var titleMatch = titleRegex.Match(matchingPodcastEpisode.Episode.Title);
            if (titleMatch.Success)
            {
                var partNumber = titleMatch.Result("${partnumber}");
                if (int.TryParse(partNumber, out _))
                {
                    orderedBundleEpisodes = (await GetOrderedBundleEpisodes(matchingPodcastEpisode)).ToArray();
                }
            }
        }

        if (orderedBundleEpisodes.Length == 0)
        {
            orderedBundleEpisodes = [matchingPodcastEpisode.Episode];
        }

        return orderedBundleEpisodes;
    }

    private async Task<IOrderedEnumerable<Episode>> GetOrderedBundleEpisodes(PodcastEpisodeV2 matchingPodcastEpisode)
    {
        if (string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast.TitleRegex))
        {
            throw new InvalidOperationException(
                $"Podcast with bundles must provide a {nameof(matchingPodcastEpisode.Podcast.TitleRegex)}. Podcast in error: id='{matchingPodcastEpisode.Podcast.Id}', name='{matchingPodcastEpisode.Podcast.Name}'. Cannot bundle episodes without a Title-Regex to collate bundles");
        }

        var podcastTitleRegex = new Regex(matchingPodcastEpisode.Podcast.TitleRegex, Podcast.TitleFlags);
        var rawTitle = podcastTitleRegex.Match(matchingPodcastEpisode.Episode.Title).Result("${title}");
        var episodes = await episodeRepository.GetByPodcastId(matchingPodcastEpisode.Podcast.Id).ToListAsync();
        var bundleEpisodes = episodes
            .Where(x => Math.Abs((matchingPodcastEpisode.Episode.Release - x.Release).Ticks) <
                        BundledEpisodeReleaseThreshold.Ticks)
            .Where(x => x.Title.Contains(rawTitle) && podcastTitleRegex.Match(x.Title).Success);
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