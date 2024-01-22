using System.Globalization;
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
    ILogger<TweetBuilder> logger)
    : ITweetBuilder
{
    private readonly TwitterOptions _twitterOptions = twitterOptions.Value;

    public async Task<string> BuildTweet(PodcastEpisode podcastEpisode)
    {
        var postModel = (podcastEpisode.Podcast, new[] {podcastEpisode.Episode}).ToPostModel();
        var episodeTitle = textSanitiser.SanitiseTitle(postModel);

        var episodeHashtags = await GetHashTags(podcastEpisode.Episode.Subjects) ?? string.Empty;
        var hashtags = episodeHashtags.Split(' ').AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_twitterOptions.HashTag))
        {
            hashtags = hashtags.Append(_twitterOptions.HashTag);
        }

        hashtags = hashtags.Where(x => !string.IsNullOrWhiteSpace(x));

        var hashtagsAdded = new List<string>();
        foreach (var hashtag in hashtags)
        {
            (episodeTitle, var addedHashTag) = hashTagEnricher.AddHashTag(episodeTitle, hashtag.TrimStart('#'));
            if (addedHashTag)
            {
                hashtagsAdded.Add(hashtag);
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
            hashtags.Where(x => !hashtagsAdded.Contains(x)).Select(x => $"#{x.TrimStart('#')}"));
        tweetBuilder.AppendLine(endHashTags);

        var permittedTitleLength = 257 - tweetBuilder.Length;

        if (episodeTitle.Length > permittedTitleLength)
        {
            episodeTitle = episodeTitle[..Math.Min(episodeTitle.Length, permittedTitleLength - 3)] + "...";
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

    private async Task<string?> GetHashTags(List<string> episodeSubjects)
    {
        var subjectRetrieval = episodeSubjects.Select(x => subjectRepository.GetByName(x)).ToArray();
        var subjects = await Task.WhenAll(subjectRetrieval);
        IEnumerable<string> hashTags = subjects.Where(x => !string.IsNullOrWhiteSpace(x?.HashTag))
            .Select(x => x!.HashTag).Distinct()!;
        if (hashTags.Any())
        {
            return string.Join(" ", hashTags);
        }

        return null;
    }
}