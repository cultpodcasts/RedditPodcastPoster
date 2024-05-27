using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlShortening;

namespace RedditPodcastPoster.Twitter;

public class TweetBuilder(
    ITextSanitiser textSanitiser,
    IHashTagEnricher hashTagEnricher,
    IHashTagProvider hashTagProvider,
    IOptions<TwitterOptions> twitterOptions,
    IOptions<ShortnerOptions> shortnerOptions,
    IShortnerService shortnerService,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<TweetBuilder> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ITweetBuilder
{
    private readonly ShortnerOptions _shortnerOptions = shortnerOptions.Value;
    private readonly TwitterOptions _twitterOptions = twitterOptions.Value;

    public async Task<string> BuildTweet(PodcastEpisode podcastEpisode)
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
            $"{podcastEpisode.Episode.Release.ToString("d MMM yyyy")} {podcastEpisode.Episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture)}");

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
            episodeTitle = episodeTitle[..Math.Min(episodeTitle.Length, permittedTitleLength - 1)] + "…";
        }

        if (_twitterOptions.WithEpisodeUrl && (podcastEpisode.HasMultipleServices() ||
                                               podcastEpisode.Podcast.Episodes.Count > 1))
        {
            var shortnerResult = await shortnerService.Write(podcastEpisode);
            var url = shortnerResult.Success
                ? $"{_shortnerOptions.ShortnerUrl}{podcastEpisode.Episode.Id.ToBase64()}"
                : podcastEpisode.ToEpisodeUrl();
            tweetBuilder.Append($"{url}{Environment.NewLine}");
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
}