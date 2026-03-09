using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Factories;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

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
        PodcastEpisode podcastEpisode,
        bool preferYouTube = false)
    {
        try
        {
            var episodes = await GetEpisodes(podcastEpisode);
            var postModel = postModelFactory.ToPostModel((podcastEpisode.Podcast, episodes), preferYouTube);
            var result = await episodePostManager.Post(postModel);

            if (result.Success)
            {
                // Load V2 episodes and mark as posted
                var episodeIds = episodes.Select(e => e.Id).ToList();
                var v2Episodes = new List<Models.V2.Episode>();

                foreach (var episodeId in episodeIds)
                {
                    var v2Episode = await episodeRepository.GetEpisode(podcastEpisode.Podcast.Id, episodeId);
                    if (v2Episode != null)
                    {
                        v2Episode.Posted = true;
                        v2Episodes.Add(v2Episode);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Episode with id '{EpisodeId}' not found in detached repository for podcast '{PodcastId}'.",
                            episodeId, podcastEpisode.Podcast.Id);
                    }
                }

                // Save updated episodes
                if (v2Episodes.Any())
                {
                    await episodeRepository.Save(v2Episodes);
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

    private async Task<Episode[]> GetEpisodes(PodcastEpisode matchingPodcastEpisode)
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

    private async Task<IOrderedEnumerable<Episode>> GetOrderedBundleEpisodes(PodcastEpisode matchingPodcastEpisode)
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
        var legacyEpisodes = v2Episodes.Select(ToLegacyEpisode).ToList();

        var bundleEpisodes = legacyEpisodes
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

    private static Episode ToLegacyEpisode(Models.V2.Episode v2Episode)
    {
        return new Episode
        {
            Id = v2Episode.Id,
            Title = v2Episode.Title,
            Description = v2Episode.Description,
            Release = v2Episode.Release,
            Length = v2Episode.Length,
            Explicit = v2Episode.Explicit,
            Posted = v2Episode.Posted,
            Tweeted = v2Episode.Tweeted,
            BlueskyPosted = v2Episode.BlueskyPosted,
            Ignored = v2Episode.Ignored,
            Removed = v2Episode.Removed,
            SpotifyId = v2Episode.SpotifyId,
            AppleId = v2Episode.AppleId,
            YouTubeId = v2Episode.YouTubeId,
            Urls = v2Episode.Urls,
            Subjects = v2Episode.Subjects,
            SearchTerms = v2Episode.SearchTerms,
            Language = v2Episode.SearchLanguage,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }
}
