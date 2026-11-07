using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.HashTags;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

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
    /// <summary>X Help recommends at most two hashtags per post; keep title + end tags to this many.</summary>
    private const int MaxHashTags = 2;
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
            if (hashtagsAdded.Count >= MaxHashTags)
            {
                break;
            }

            if (!hashtagsAdded.Select(x => x.ToLowerInvariant()).Contains(hashtag.Tag.ToLowerInvariant()))
            {
                // Decide from the stored tag. MatchingText can already be a shortened
                // form (Some-Tag → #SomeTag). The text written after '#' must be that stored body.
                if (!HashtagText.IsReproducedAsIs(hashtag.Tag))
                {
                    continue;
                }

                var tagText = HashtagText.ToTagText(hashtag.Tag)!;
                (episodeTitle, var addedHashTag) =
                    hashTagEnricher.AddHashTag(
                        episodeTitle,
                        hashtag.Tag.TrimStart('#'),
                        tagText);
                if (addedHashTag)
                {
                    hashtagsAdded.Add(hashtag.Tag);
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
        if (SocialPostKindTemplate.HasParentLine(podcastEpisode))
        {
            if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.TwitterHandle))
            {
                tweetBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.TwitterHandle}{guestHandles}");
            }
            else
            {
                tweetBuilder.AppendLine($"{podcastName}{guestHandles}");
            }
        }
        else
        {
            // Film: no parent name; the film's own handle and guests still get tagged.
            var filmHandles = string.Join(" ",
                new[] { podcastEpisode.Podcast.TwitterHandle }.Where(x => !string.IsNullOrWhiteSpace(x))
                    .Concat(guestHandlesToAppend));
            if (!string.IsNullOrWhiteSpace(filmHandles))
            {
                tweetBuilder.AppendLine(filmHandles);
            }
        }

        tweetBuilder.AppendLine(SocialPostKindTemplate.ReleaseLine(podcastEpisode));

        var endHashTagSlots = MaxHashTags - hashtagsAdded.Count;
        var endHashTags = endHashTagSlots <= 0
            ? string.Empty
            : string.Join(" ",
                episodeHashtags
                    .Where(x => x.MatchingText == null)
                    .Select(x => x.Tag)
                    .Distinct()
                    .Where(x => !hashtagsAdded.Contains(x))
                    .Where(HashtagText.IsReproducedAsIs)
                    .Take(endHashTagSlots)
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
            && (SocialPostKindTemplate.AlwaysLinksSite(podcastEpisode) ||
                podcastEpisode.HasMultipleServices() || podcastEpisode.Episode.Subjects.Any());
        var permittedTitleLength = 257 - (tweetBuilder.Length + (reserveSecondUrl ? 26 : 0));

        if (episodeTitle.Length > permittedTitleLength)
        {
            var min = Math.Min(episodeTitle.Length, permittedTitleLength - 1);
            if (min < MinTitleLength)
            {
                throw new UntweetableException(
                    TweetSkipReasons.TitleTooLong,
                    $"Unable to form tweet body from '\"{episodeTitle}\"{Environment.NewLine}{tweetBuilder}', calculated title-length: {min} which is less than {MinTitleLength}.");
            }

            episodeTitle = episodeTitle[..min] + "…";
        }

        tweetBuilder.Insert(0, $"\"{episodeTitle}\"{Environment.NewLine}");

        if (shortUrlOnly && shortUrl != null)
        {
            tweetBuilder.Append(shortUrl.AbsoluteUri);
            return tweetBuilder.ToString();
        }

        if (!EpisodeServicePresence.TryGetPreferredSocialPostUrl(
                podcastEpisode.Episode,
                StreamingServiceCatalog.ImageCoalesceOrder,
                out var postUrl,
                out _))
        {
            throw new UntweetableException(TweetSkipReasons.NoLink, "No link found to tweet");
        }

        tweetBuilder.Append(postUrl);

        if (shortUrl != null && _twitterOptions.WithEpisodeUrl && (SocialPostKindTemplate.AlwaysLinksSite(podcastEpisode) ||
                                                           podcastEpisode.HasMultipleServices() ||
                                                           podcastEpisode.Episode.Subjects.Any()))
        {
            tweetBuilder.Append($"{Environment.NewLine}{shortUrl.AbsoluteUri}");
        }

        return tweetBuilder.ToString();
    }
}
