using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;

namespace RedditPodcastPoster.Twitter.Builders;

public class TweetBuilder(
    ITextSanitiser textSanitiser,
    IHashTagEnricher hashTagEnricher,
    IHashTagProvider hashTagProvider,
    IPostModelFactory postModelFactory,
    IPersonGuestHandleResolver personGuestHandleResolver,
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

    public async Task<string> BuildTweet(PodcastEpisode podcastEpisode, Uri? shortUrl, bool hasShareImage = false)
    {
        var postModel = postModelFactory.ToPostModel((podcastEpisode.Podcast, [podcastEpisode.Episode]));
        var episodeTitle = await textSanitiser.SanitiseTitle(postModel);

        var episodeHashtags = await hashTagProvider.GetHashTags(podcastEpisode.Episode.Subjects);
        episodeHashtags = episodeHashtags.Union(podcastEpisode.Podcast.GetHashTags()).ToList();
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Episode.HashTag))
        {
            episodeHashtags = episodeHashtags.Union(podcastEpisode.Episode.HashTag.ToHashTags()).ToList();
        }
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
        var (twitterHandles, _) = await personGuestHandleResolver.Resolve(podcastEpisode.Episode);
        var guestHandlesToAppend = SocialHandleDeduplicator.Deduplicate(
            twitterHandles,
            alreadyTagged: [podcastEpisode.Podcast.TwitterHandle]);
        var guestHandles = guestHandlesToAppend.Length > 0
            ? " " + string.Join(" ", guestHandlesToAppend)
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.TwitterHandle))
        {
            tweetBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.TwitterHandle}{guestHandles}");
        }
        else
        {
            tweetBuilder.AppendLine($"{podcastName}{guestHandles}");
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

        // 257 already budgets one t.co URL. Extra 26 only when a second (short) URL will also be appended.
        // Share-image short-URL-only posts need no second-URL reserve (gated by ShortUrlOnlyWhenShareImage).
        var shortUrlOnly = hasShareImage && _twitterOptions.ShortUrlOnlyWhenShareImage;
        var reserveSecondUrl = !shortUrlOnly
            && _twitterOptions.WithEpisodeUrl
            && (podcastEpisode.HasMultipleServices() || podcastEpisode.Episode.Subjects.Any());
        var permittedTitleLength = 257 - (tweetBuilder.Length + (reserveSecondUrl ? 26 : 0));

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

        tweetBuilder.Insert(0, $"\"{episodeTitle}\"{Environment.NewLine}");

        if (shortUrlOnly && shortUrl != null)
        {
            tweetBuilder.Append(shortUrl);
            return tweetBuilder.ToString();
        }

        if (!EpisodeServicePresence.TryGetPreferredSocialPostUrl(
                podcastEpisode.Episode, out var postUrl, out _))
        {
            throw new InvalidOperationException("No link found to tweet");
        }

        tweetBuilder.Append(postUrl);

        if (shortUrl != null && _twitterOptions.WithEpisodeUrl && (podcastEpisode.HasMultipleServices() ||
                                                           podcastEpisode.Episode.Subjects.Any()))
        {
            tweetBuilder.Append($"{Environment.NewLine}{shortUrl}");
        }

        return tweetBuilder.ToString();
    }
}
