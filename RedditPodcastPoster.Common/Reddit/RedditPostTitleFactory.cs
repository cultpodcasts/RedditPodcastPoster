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
    private readonly Regex _withName = new Regex(@"(?'before'\s)(?'with'[Ww]ith )(?'after'[A-Z])");
    private readonly TextInfo _textInfo= new CultureInfo("en-GB", false).TextInfo;
    private readonly Regex _invalidTitlePrefix = new Regex(@"(?'prefix'^[^a-zA-Z\d""]+)(?'after'.*$)");

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
        var episodeTitle = postModel.EpisodeTitle;
        if (postModel.TitleRegex != null)
        {
            episodeTitle = _textSanitiser.ExtractTitle(episodeTitle, postModel.TitleRegex);
        }

        var title = ConstructBasePostTitle(postModel, episodeTitle);
        var bundleSuffix = CreateBundleSuffix(postModel.BundledPartNumbers);
        var audioLinksSuffix = "";
        if (postModel.HasYouTubeUrl && (postModel.Spotify != null || postModel.Apple != null))
        {
            audioLinksSuffix = "(Audio links in comments)";
        }

        return ConstructFinalPostTitle(title, bundleSuffix, audioLinksSuffix);
    }

    private string ConstructFinalPostTitle(string title, string bundleSuffix, string audioLinksSuffix)
    {
        const string ellipsisSuffix = @"..""";
        const string terminalSuffix = @"""";
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

    private string ConstructBasePostTitle(PostModel postModel, string episodeTitle)
    {
        var podcastName = postModel.PodcastName;
        var description = _textSanitiser.Sanitise(postModel.EpisodeDescription);
        if (postModel.DescriptionRegex != null)
        {
            description = _textSanitiser.ExtractBody(description, postModel.DescriptionRegex);
        }

        episodeTitle = _textSanitiser.FixCharacters(episodeTitle);
        podcastName = _textSanitiser.FixCharacters(podcastName);
        description = _textSanitiser.FixCharacters(description);


        var withMatch = _withName.Match(episodeTitle).Groups["with"];
        if (withMatch.Success)
        {
            episodeTitle= _withName.Replace(episodeTitle, "${before}w/${after}");
        }
        var invalidPrefixMatch= _invalidTitlePrefix.Match(episodeTitle).Groups["prefix"];
        if (invalidPrefixMatch.Success)
        {
            episodeTitle = _invalidTitlePrefix.Replace(episodeTitle, "${after}");
        }

        episodeTitle = _textInfo.ToTitleCase(episodeTitle.ToLower());
        podcastName= _textInfo.ToTitleCase(podcastName.ToLower());

        episodeTitle = _textSanitiser.FixCasing(episodeTitle);

        return $"\"{episodeTitle}\", {podcastName}, {postModel.ReleaseDate} {postModel.EpisodeLength} \"{description}";
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