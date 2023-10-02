using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Common.Text;

namespace RedditPodcastPoster.Common.Reddit;

public class RedditPostTitleFactory : IRedditPostTitleFactory
{
    private readonly ILogger<RedditPostTitleFactory> _logger;
    private readonly SubredditSettings _settings;
    private readonly ITextSanitiser _textSanitiser;

    public RedditPostTitleFactory(
        ITextSanitiser textSanitiser,
        IOptions<SubredditSettings> settings,
        ILogger<RedditPostTitleFactory> logger)
    {
        _textSanitiser = textSanitiser;
        _settings = settings.Value;
        _logger = logger;
    }

    public string ConstructPostTitle(PostModel postModel)
    {
        var title = ConstructBasePostTitle(postModel);
        var bundleSuffix = CreateBundleSuffix(postModel.BundledPartNumbers);
        var audioLinksSuffix = "";
        if (postModel.HasYouTubeUrl && (postModel.Spotify != null || postModel.Apple != null))
        {
            audioLinksSuffix = "(Audio links in comments)";
        }

        return ConstructFinalPostTitle(title, !string.IsNullOrWhiteSpace(postModel.EpisodeDescription), bundleSuffix,
            audioLinksSuffix);
    }

    private string ConstructFinalPostTitle(string title, bool hasDescription, string bundleSuffix,
        string audioLinksSuffix)
    {
        var ellipsisSuffix = hasDescription ? @"..""" : string.Empty;
        var terminalSuffix = hasDescription ? @"""" : string.Empty;
        if ((title + bundleSuffix + audioLinksSuffix).Length >
            _settings.SubredditTitleMaxLength - terminalSuffix.Length)
        {
            title =
                title.Substring(0,
                    _settings.SubredditTitleMaxLength -
                    (ellipsisSuffix.Length + bundleSuffix.Length + audioLinksSuffix.Length)) +
                $"{ellipsisSuffix}{bundleSuffix}{audioLinksSuffix}";
        }
        else
        {
            title += $"{terminalSuffix}{bundleSuffix}{audioLinksSuffix}";
        }

        return title;
    }

    private string ConstructBasePostTitle(PostModel postModel)
    {
        var episodeTitle = _textSanitiser.SanitiseTitle(postModel);
        var podcastName = _textSanitiser.SanitisePodcastName(postModel);
        var description = _textSanitiser.SanitiseDescription(postModel);
        var title = $"\"{episodeTitle}\", {podcastName}, {postModel.ReleaseDate} {postModel.EpisodeLength}";
        if (!string.IsNullOrWhiteSpace(description))
        {
            title += $" \"{description}";
        }

        return title;
    }

    private static string CreateBundleSuffix(IEnumerable<int>? partNumbers)
    {
        var bundleSuffix = string.Empty;
        if (partNumbers == null || partNumbers.Count() <= 1)
        {
            return bundleSuffix;
        }

        var range = partNumbers.Count() switch
        {
            2 => $"Pt.{partNumbers.Last()}",
            3 => $"Pts.{partNumbers.Skip(1).First()}&{partNumbers.Last()}",
            _ => $"Pts.{partNumbers.Skip(1).First()}-{partNumbers.Last()}"
        };
        bundleSuffix = $"(Links to {range} in comments)";

        return bundleSuffix;
    }
}