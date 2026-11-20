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

    /// <summary>X's published tweet character limit.</summary>
    public const int XTweetCharacterLimit = 280;

    /// <summary>X counts each http(s) URL as this many characters (t.co wrapping).</summary>
    public const int TcoUrlCharacterWeight = 23;

    /// <summary>
    /// Soft character budget for the tweet body before the primary platform URL is appended.
    /// Each http(s) link is counted as a <see cref="TcoUrlCharacterWeight"/>-character t.co URL, so
    /// <see cref="XTweetCharacterLimit"/> − that weight leaves room for one link. When a second
    /// (short) site URL will also be appended, reserve another <see cref="SecondUrlReserve"/> characters.
    /// </summary>
    public const int CharacterBudgetBeforePrimaryUrl = XTweetCharacterLimit - TcoUrlCharacterWeight;

    /// <summary>
    /// Extra reserve when a short URL will follow the primary link: one t.co
    /// (<see cref="TcoUrlCharacterWeight"/>) plus a small separator allowance for the newline
    /// before the short URL (kept at 26 to match the long-standing title-trim budget).
    /// </summary>
    public const int SecondUrlReserve = TcoUrlCharacterWeight + 3;

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
                // Multi-word enrichment tags: MatchingText holds the compacted permitted form
                // ("Alpha Beta" → "#AlphaBeta"). Invalid tags (&, +, -) are filtered at accept
                // (ToHashTags / FromEnrichmentHashTagsToHashTags) and must not be rewritten here.
                var tagText = HashtagText.GetTitleEnrichmentTagText(hashtag.Tag, hashtag.MatchingText);
                if (tagText is null)
                {
                    continue;
                }

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
        string[] guestHandlesToAppend = [];
        if (_twitterOptions.IncludeAtMentions)
        {
            var (twitterHandles, _) = await personGuestHandleResolver.Resolve(podcastEpisode.Episode);
            guestHandlesToAppend = SocialHandleDeduplicator.Deduplicate(
                twitterHandles,
                alreadyTagged: [podcastEpisode.Podcast.TwitterHandle]);
        }

        var guestHandles = guestHandlesToAppend.Length > 0
            ? " " + string.Join(" ", guestHandlesToAppend)
            : string.Empty;
        if (SocialPostKindTemplate.HasParentLine(podcastEpisode))
        {
            if (_twitterOptions.IncludeAtMentions
                && !string.IsNullOrWhiteSpace(podcastEpisode.Podcast.TwitterHandle))
            {
                tweetBuilder.AppendLine($"{podcastName} {podcastEpisode.Podcast.TwitterHandle}{guestHandles}");
            }
            else
            {
                tweetBuilder.AppendLine($"{podcastName}{guestHandles}");
            }
        }
        else if (_twitterOptions.IncludeAtMentions)
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

        var endHashTags = string.Join(" ",
            episodeHashtags
                .Where(x => x.MatchingText == null)
                .Select(x => x.Tag)
                .Distinct()
                .Where(x => !hashtagsAdded.Contains(x))
                .Where(HashtagText.IsReproducedAsIs)
                .Select(x => $"#{x.TrimStart('#')}"));
        if (!string.IsNullOrWhiteSpace(endHashTags))
        {
            tweetBuilder.AppendLine(endHashTags);
        }

        // CharacterBudgetBeforePrimaryUrl already budgets one t.co URL. SecondUrlReserve only when a
        // second (short) URL will also be appended. Share-image short-URL-only posts need no second-URL
        // reserve (gated by ShortUrlOnlyWhenShareImage).
        var shortUrlOnly = hasShareImage && _twitterOptions.ShortUrlOnlyWhenShareImage;
        var reserveSecondUrl = !shortUrlOnly
            && _twitterOptions.WithEpisodeUrl
            && (SocialPostKindTemplate.AlwaysLinksSite(podcastEpisode) ||
                podcastEpisode.HasMultipleServices() || podcastEpisode.Episode.Subjects.Any());
        var permittedTitleLength = CharacterBudgetBeforePrimaryUrl -
                                   (tweetBuilder.Length + (reserveSecondUrl ? SecondUrlReserve : 0));

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
