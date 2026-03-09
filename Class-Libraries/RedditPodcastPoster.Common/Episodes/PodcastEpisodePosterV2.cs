using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Factories;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.Common.Episodes;

/// <summary>
/// V2 implementation that posts podcast episodes and updates their status in detached IEpisodeRepository.
/// </summary>
public class PodcastEpisodePosterV2(
    IEpisodePostManager episodePostManager,
    IPostModelFactory postModelFactory,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastEpisodePosterV2> logger
) : IPodcastEpisodePosterV2
{
    private static readonly TimeSpan BundledEpisodeReleaseThreshold = TimeSpan.FromDays(7);

    public async Task<ProcessResponse> PostPodcastEpisode(
        PodcastEpisodeV2 podcastEpisode,
        bool preferYouTube = false)
    {
        try
        {
            var v2Episodes = await GetEpisodesV2(podcastEpisode);
            
            var postModel = postModelFactory.ToPostModel((podcastEpisode.Podcast, v2Episodes), preferYouTube);
            var result = await episodePostManager.Post(postModel);

            if (result.Success)
            {
                // Mark V2 episodes as posted
                foreach (var v2Episode in v2Episodes)
                {
                    v2Episode.Posted = true;
                }

                // Save updated episodes
                await episodeRepository.Save(v2Episodes);
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

    private async Task<List<Models.V2.Episode>> GetEpisodesV2(PodcastEpisodeV2 matchingPodcastEpisode)
    {
        var orderedBundleEpisodes = new List<Models.V2.Episode>();

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
                    orderedBundleEpisodes = (await GetOrderedBundleEpisodesV2(matchingPodcastEpisode)).ToList();
                }
            }
        }

        if (orderedBundleEpisodes.Count == 0)
        {
            orderedBundleEpisodes = [matchingPodcastEpisode.Episode];
        }

        return orderedBundleEpisodes;
    }

    private async Task<IOrderedEnumerable<Models.V2.Episode>> GetOrderedBundleEpisodesV2(PodcastEpisodeV2 matchingPodcastEpisode)
    {
        if (string.IsNullOrWhiteSpace(matchingPodcastEpisode.Podcast.TitleRegex))
        {
            throw new InvalidOperationException(
                $"Podcast with bundles must provide a {nameof(matchingPodcastEpisode.Podcast.TitleRegex)}. Podcast in error: id='{matchingPodcastEpisode.Podcast.Id}', name='{matchingPodcastEpisode.Podcast.Name}'. Cannot bundle episodes without a Title-Regex to collate bundles");
        }

        var podcastTitleRegex = new Regex(matchingPodcastEpisode.Podcast.TitleRegex, Podcast.TitleFlags);
        var rawTitle = podcastTitleRegex.Match(matchingPodcastEpisode.Episode.Title).Result("${title}");
        
        // Load episodes from detached repository
        var v2Episodes = await episodeRepository.GetByPodcastId(matchingPodcastEpisode.Podcast.Id).ToListAsync();

        var bundleEpisodes = v2Episodes
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
