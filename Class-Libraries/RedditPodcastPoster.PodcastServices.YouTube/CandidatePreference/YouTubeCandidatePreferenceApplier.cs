using System.Text.RegularExpressions;
using FuzzySharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Shared by both YouTube finder decorators. After a finder has picked a match, looks for other
/// candidates in the same list that are the same episode (similar title, similar duration, passing the
/// same duration and strong-match validation). Only when at least one such contender exists does it ask
/// <see cref="IYouTubeCandidatePreference"/> to choose, so single-candidate lookups cost nothing extra.
/// </summary>
public partial class YouTubeCandidatePreferenceApplier(
    IYouTubeServiceWrapper youTubeService,
    ITolerantYouTubeVideoService videoService,
    IYouTubeCandidatePreference preference,
    IOptions<YouTubeCandidatePreferenceOptions> options,
    ILogger<YouTubeCandidatePreferenceApplier> logger)
{
    private static readonly Regex Whitespace = CreateWhitespace();

    public async Task<FindEpisodeResponse?> ApplyAsync<TItem>(
        EpisodeModel episode,
        IList<TItem> items,
        FindEpisodeResponse? chosenResponse,
        TItem? chosenItem,
        Podcast scoringPodcast,
        IndexingContext indexingContext,
        Func<TItem, string> videoIdOf,
        Func<TItem, string> titleOf,
        Func<TItem, Google.Apis.YouTube.v3.Data.Video, EpisodeModel> toEpisode,
        Func<TItem, Google.Apis.YouTube.v3.Data.Video, FindEpisodeResponse> toResponse)
        where TItem : class
    {
        var settings = options.Value;
        if (!settings.Enabled || chosenResponse == null || chosenItem == null || items.Count < 2)
        {
            return chosenResponse;
        }

        var chosenId = videoIdOf(chosenItem);
        var chosenTitle = Normalise(titleOf(chosenItem), settings);
        var contenderItems = items
            .Where(x => videoIdOf(x) != chosenId)
            .GroupBy(videoIdOf)
            .Select(g => g.First())
            .Select(x => (Item: x, Similarity: Fuzz.TokenSetRatio(chosenTitle, Normalise(titleOf(x), settings))))
            .Where(x => x.Similarity >= settings.ContenderMinTitleSimilarity)
            .OrderByDescending(x => x.Similarity)
            .Take(settings.MaxContenders)
            .Select(x => x.Item)
            .ToList();
        if (contenderItems.Count == 0)
        {
            return chosenResponse;
        }

        var videos = await videoService.GetVideoContentDetails(
            youTubeService, contenderItems.Select(videoIdOf).ToList(), indexingContext, withSnippets: true);
        if (videos == null || videos.Count == 0)
        {
            return chosenResponse;
        }

        var chosenLength = chosenResponse.Video?.GetLength();
        var maxDifference = TimeSpan.FromSeconds(settings.ContenderMaxDurationDifferenceSeconds);
        var validated = new List<(YouTubeCandidate Candidate, TItem Item, Google.Apis.YouTube.v3.Data.Video Video)>();
        foreach (var item in contenderItems)
        {
            var video = videos.FirstOrDefault(v => v.Id == videoIdOf(item));
            if (video == null || !video.IsCompletedPublicVideo())
            {
                continue;
            }

            var length = video.GetLength();
            if (!YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(episode.Length, length))
            {
                continue;
            }

            if (chosenLength.HasValue && length.HasValue && (length.Value - chosenLength.Value).Duration() > maxDifference)
            {
                continue;
            }

            if (!YouTubeEnrichmentCandidate.MeetsStrongMatch(episode, toEpisode(item, video), scoringPodcast))
            {
                continue;
            }

            validated.Add((new YouTubeCandidate(videoIdOf(item), titleOf(item), length), item, video));
        }

        if (validated.Count == 0)
        {
            return chosenResponse;
        }

        var chosen = new YouTubeCandidate(chosenId, titleOf(chosenItem), chosenLength);
        var winnerId = await preference.ChooseAsync(chosen, validated.Select(x => x.Candidate).ToList());
        if (winnerId == chosenId)
        {
            return chosenResponse;
        }

        var winner = validated.Single(x => x.Candidate.VideoId == winnerId);
        logger.LogInformation(
            "YouTube candidate preference replaced '{chosenVideoId}' ('{chosenTitle}') with '{winnerVideoId}' ('{winnerTitle}') for episode '{episodeId}' ('{episodeTitle}').",
            chosenId, chosen.Title, winnerId, winner.Candidate.Title, episode.Id, episode.Title);
        return toResponse(winner.Item, winner.Video);
    }

    internal static string Normalise(string? title, YouTubeCandidatePreferenceOptions settings)
    {
        var text = title ?? string.Empty;
        foreach (var token in settings.PenalisedTitleTokens.Where(t => !string.IsNullOrEmpty(t)))
        {
            text = text.Replace(token, " ", StringComparison.Ordinal);
        }

        return Whitespace.Replace(text, " ").Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CreateWhitespace();
}