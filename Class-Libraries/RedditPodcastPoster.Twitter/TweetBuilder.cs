using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Twitter;

public class TweetBuilder(
    ITextSanitiser textSanitiser,
    IHashTagEnricher hashTagEnricher,
    IHashTagProvider hashTagProvider,
    IOptions<TwitterOptions> twitterOptions,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<TweetBuilder> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ITweetBuilder
{
    private const int MinTitleLength = 10;
    public const string LengthFormat = @"\[h\:mm\:ss\]";
    public const string? ReleaseFormat = "d MMM yyyy";
    private readonly TwitterOptions _twitterOptions = twitterOptions.Value;

    public async Task<string> BuildTweet(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var postModel = (podcastEpisode.Podcast, new[] {podcastEpisode.Episode}).ToPostModel();
        var episodeTitle = textSanitiser.SanitiseTitle(postModel);

        var episodeHashtags = await hashTagProvider.GetHashTags(podcastEpisode.Episode.Subjects);
        if (!string.IsNullOrWhiteSpace(_twitterOptions.HashTag))
        {
            episodeHashtags.Add(new HashTag(_twitterOptions.HashTag, null));
        }

        var hashtagsAdded = new List<string>();
        foreach (var hashtag in episodeHashtags)
        {
            if (!hashtagsAdded.Select(x => x.ToLowerInvariant()).Contains(hashtag.Tag.ToLowerInvariant()))
            {
                (episodeTitle, var addedHashTag) =
                    hashTagEnricher.AddHashTag(
                        episodeTitle,
                        hashtag.Tag.TrimStart('#'),
                        hashtag.MatchingText?.TrimStart('#'));
                if (addedHashTag)
                {
                    hashtagsAdded.Add(hashtag.MatchingText ?? hashtag.Tag);
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
            $"{podcastEpisode.Episode.Release.ToString(ReleaseFormat)} {podcastEpisode.Episode.Length.ToString(LengthFormat, CultureInfo.InvariantCulture)}");

        var endHashTags = string.Join(" ",
            episodeHashtags
                .Where(x => x.MatchingText == null)
                .Select(x => x.Tag)
                .Distinct()
                .Where(x => !hashtagsAdded.Contains(x))
                .Select(x => $"#{x.TrimStart('#')}"));
        if (!string.IsNullOrWhiteSpace(endHashTags))
        {
            tweetBuilder.AppendLine(endHashTags);
        }

        var permittedTitleLength = 257 - (tweetBuilder.Length + (_twitterOptions.WithEpisodeUrl ? 26 : 0));

        if (episodeTitle.Length > permittedTitleLength)
        {
            var min = Math.Min(episodeTitle.Length, permittedTitleLength - 1);
            if (min < MinTitleLength)
            {
                throw new InvalidOperationException(
                    $"Unable to form tweet body from '\"{episodeTitle}\"{Environment.NewLine}{tweetBuilder}', calculated title-length: {min} which is less than {MinTitleLength}.");
            }

            episodeTitle = episodeTitle[..min] + "…";
        }

        if (shortUrl != null && _twitterOptions.WithEpisodeUrl && (podcastEpisode.HasMultipleServices() ||
                                                                   podcastEpisode.Podcast.Episodes.Count > 1 ||
                                                                   podcastEpisode.Episode.Subjects.Any()))
        {
            tweetBuilder.Append($"{shortUrl}{Environment.NewLine}");
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
        else if (podcastEpisode.Episode.Urls.Apple != null)
        {
            tweetBuilder.Append(podcastEpisode.Episode.Urls.Apple!);
        }
        else if (podcastEpisode.Episode.Urls.InternetArchive != null)
        {
            tweetBuilder.Append(podcastEpisode.Episode.Urls.InternetArchive!);
        }
        else if (podcastEpisode.Episode.Urls.BBC != null)
        {
            tweetBuilder.Append(podcastEpisode.Episode.Urls.BBC!);
        }
        else
        {
            throw new InvalidOperationException("No link found to tweet");
        }

        var tweet = tweetBuilder.ToString();
        return tweet;
    }
}