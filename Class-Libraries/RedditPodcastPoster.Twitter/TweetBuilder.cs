﻿using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Twitter;

public class TweetBuilder(
    ITextSanitiser textSanitiser,
    ISubjectRepository subjectRepository,
    IHashTagEnricher hashTagEnricher,
    IOptions<TwitterOptions> twitterOptions,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<TweetBuilder> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ITweetBuilder
{
    private static readonly TextInfo TextInfo = new CultureInfo("en-GB", false).TextInfo;
    private readonly TwitterOptions _twitterOptions = twitterOptions.Value;

    public async Task<string> BuildTweet(PodcastEpisode podcastEpisode)
    {
        var postModel = (podcastEpisode.Podcast, new[] {podcastEpisode.Episode}).ToPostModel();
        var episodeTitle = textSanitiser.SanitiseTitle(postModel);

        var episodeHashtags = await GetHashTags(podcastEpisode.Episode.Subjects);
        if (!string.IsNullOrWhiteSpace(_twitterOptions.HashTag))
        {
            episodeHashtags.Add((_twitterOptions.HashTag, null));
        }

        var hashtagsAdded = new List<string>();
        foreach (var hashtag in episodeHashtags)
        {
            if (!hashtagsAdded.Select(x => x.ToLowerInvariant()).Contains(hashtag.HashTag.ToLowerInvariant()))
            {
                (episodeTitle, var addedHashTag) =
                    hashTagEnricher.AddHashTag(
                        episodeTitle,
                        hashtag.HashTag.TrimStart('#'),
                        hashtag.EnrichmentHashTag?.TrimStart('#'));
                if (addedHashTag)
                {
                    hashtagsAdded.Add(hashtag.EnrichmentHashTag ?? hashtag.HashTag);
                }
            }
        }

        var podcastName = textSanitiser.SanitisePodcastName(postModel);

        var tweetBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.TwitterHandle))
        {
            tweetBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.TwitterHandle}");
        }
        else
        {
            tweetBuilder.AppendLine($"{podcastName}");
        }

        tweetBuilder.AppendLine(
            $"{podcastEpisode.Episode.Release.ToString("d MMM yyyy")} {podcastEpisode.Episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture)}");

        var endHashTags = string.Join(" ",
            episodeHashtags
                .Where(x => x.EnrichmentHashTag == null)
                .Select(x => x.HashTag)
                .Distinct()
                .Where(x => !hashtagsAdded.Contains(x))
                .Select(x => $"#{x.TrimStart('#')}"));
        tweetBuilder.AppendLine(endHashTags);

        var permittedTitleLength = 257 - tweetBuilder.Length;

        if (episodeTitle.Length > permittedTitleLength)
        {
            episodeTitle = episodeTitle[..Math.Min(episodeTitle.Length, permittedTitleLength - 1)] + "…";
        }

        tweetBuilder.Insert(0, $"\"{episodeTitle}\"{Environment.NewLine}");
        if (podcastEpisode.Episode.Urls.YouTube != null)
        {
            tweetBuilder.Append(podcastEpisode.Episode.Urls.YouTube);
        }
        else if (podcastEpisode.Episode.Urls.Spotify != null)
        {
            tweetBuilder.Append(podcastEpisode.Episode.Urls.Spotify);
        }
        else
        {
            tweetBuilder.Append(podcastEpisode.Episode.Urls.Apple!);
        }

        var tweet = tweetBuilder.ToString();
        return tweet;
    }

    private async Task<ICollection<(string HashTag, string? EnrichmentHashTag)>> GetHashTags(
        List<string> episodeSubjects)
    {
        var subjectRetrieval = episodeSubjects.Select(x => subjectRepository.GetByName(x)).ToArray();
        var subjects = await Task.WhenAll(subjectRetrieval);
        var hashTags =
            subjects
                .Where(x => !string.IsNullOrWhiteSpace(x?.HashTag))
                .Select(x => x!.HashTag!.Split(" "))
                .SelectMany(x => x)
                .Distinct()
                .Select(x => (x!, (string?) null));
        var enrichmentHashTags =
            subjects
                .Where(x => x?.EnrichmentHashTags != null && x.EnrichmentHashTags.Any())
                .SelectMany(x => x!.EnrichmentHashTags!)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .Select(x => (x, (string?) $"#{x.Replace(" ", string.Empty)}"));
        return hashTags.Union(enrichmentHashTags).ToList();
    }
}