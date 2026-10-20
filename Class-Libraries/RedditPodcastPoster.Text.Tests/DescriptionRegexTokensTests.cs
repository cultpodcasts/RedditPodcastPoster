using System.Text.RegularExpressions;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests;

public class DescriptionRegexTokensTests
{
    private const string Title = "Cheryl Hope: Trafficked at Age 6 — Breaking the Silence on Exclusive Brethren";
    private const string PodcastName = "Cultimatum-The Culture of Cults & Human Trafficking";
    private const string Advisory = "CONTENT ADVISORY: Discussions of trafficking and abuse.";

    // SanitiseDescription additionally applies FixCharacters (" and " becomes " & ").
    private const string AdvisoryAfterFixCharacters = "CONTENT ADVISORY: Discussions of trafficking & abuse.";

    private const string StripTitlePattern = @"^\s*{episode-title}\s*[-–—:|]?\s*(?<body>.*)$";

    private readonly AutoMocker _mocker = new();

    public DescriptionRegexTokensTests()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    private string Extract(string description, string pattern, string? title = Title, string? podcast = PodcastName)
    {
        return Sut.ExtractDescription(description, pattern, title, podcast);
    }

    [Fact(DisplayName = "EpisodeTitleToken_TitleDirectlyFollowedByDescription_StripsTitle")]
    public void EpisodeTitleToken_TitleDirectlyFollowedByDescription_StripsTitle()
    {
        Extract(Title + Advisory, StripTitlePattern).Should().Be(Advisory);
    }

    [Theory(DisplayName = "EpisodeTitleToken_SeparatorVariants_StripsTitleAndSeparator")]
    [InlineData(" - ")]
    [InlineData(" – ")]
    [InlineData(" — ")]
    [InlineData(": ")]
    [InlineData(" | ")]
    [InlineData("\r\n\r\n")]
    [InlineData("   ")]
    [InlineData("")]
    public void EpisodeTitleToken_SeparatorVariants_StripsTitleAndSeparator(string separator)
    {
        Extract("  " + Title + separator + Advisory, StripTitlePattern).Should().Be(Advisory);
    }

    [Fact(DisplayName = "EpisodeTitleToken_TitleAbsentFromDescription_LeavesDescriptionUnchanged")]
    public void EpisodeTitleToken_TitleAbsentFromDescription_LeavesDescriptionUnchanged()
    {
        const string description = "A different description that does not start with the title.";
        Extract(description, StripTitlePattern).Should().Be(description);
    }

    [Fact(DisplayName = "EpisodeTitleToken_TitleOnlyInMiddleOfDescription_LeavesDescriptionUnchanged")]
    public void EpisodeTitleToken_TitleOnlyInMiddleOfDescription_LeavesDescriptionUnchanged()
    {
        var description = "Intro. " + Title + " " + Advisory;
        Extract(description, StripTitlePattern).Should().Be(description);
    }

    [Fact(DisplayName = "EpisodeTitleToken_IsCaseInsensitive")]
    public void EpisodeTitleToken_IsCaseInsensitive()
    {
        Extract(Title.ToUpperInvariant() + " " + Advisory, StripTitlePattern).Should().Be(Advisory);
    }

    [Fact(DisplayName = "EpisodeTitleToken_MultilineBodyIsKept_BecauseSinglelineApplies")]
    public void EpisodeTitleToken_MultilineBodyIsKept_BecauseSinglelineApplies()
    {
        Extract(Title + "\r\nLine one.\r\nLine two.", StripTitlePattern).Should().Be("Line one.\r\nLine two.");
    }

    [Fact(DisplayName = "EpisodeTitleToken_WhitespaceRunsInDescription_StillMatch")]
    public void EpisodeTitleToken_WhitespaceRunsInDescription_StillMatch()
    {
        Extract("Cheryl  Hope:\r\nTrafficked   at Age 6 — Breaking the Silence on Exclusive Brethren " + Advisory,
            StripTitlePattern).Should().Be(Advisory);
    }

    [Fact(DisplayName = "EpisodeTitleToken_WhitespaceInTitle_IsNormalised")]
    public void EpisodeTitleToken_WhitespaceInTitle_IsNormalised()
    {
        Extract("Hello World " + Advisory, StripTitlePattern, "  Hello \t World \n").Should().Be(Advisory);
    }

    [Theory(DisplayName = "EpisodeTitleToken_RegexMetacharactersInTitle_AreMatchedLiterally")]
    [InlineData("What? (Part 1) [Live] * + . $ ^ | \\ {x} #1")]
    [InlineData("a.b")]
    [InlineData("(?:evil)|x")]
    [InlineData(".*")]
    public void EpisodeTitleToken_RegexMetacharactersInTitle_AreMatchedLiterally(string title)
    {
        Extract(title + " " + Advisory, StripTitlePattern, title).Should().Be(Advisory);
    }

    [Fact(DisplayName = "EpisodeTitleToken_MetacharactersInTitle_DoNotMatchOtherText")]
    public void EpisodeTitleToken_MetacharactersInTitle_DoNotMatchOtherText()
    {
        const string description = "axb Some description";
        Extract(description, StripTitlePattern, "a.b").Should().Be(description);
    }

    [Fact(DisplayName = "EpisodeTitleToken_TitleCannotInjectCapturingGroups")]
    public void EpisodeTitleToken_TitleCannotInjectCapturingGroups()
    {
        var regex = DescriptionRegexTokens.Create(StripTitlePattern, "(?<body>evil)", null)!;
        regex.GetGroupNames().Should().BeEquivalentTo("0", "body");
    }

    [Theory(DisplayName = "EpisodeTitleToken_TypographicAndEntityVariants_Match")]
    [InlineData("Tom & Jerry's \"Big\" Day - Part 2", "Tom &amp; Jerry&#39;s &quot;Big&quot; Day &ndash; Part 2")]
    [InlineData("Tom & Jerry's \"Big\" Day - Part 2", "Tom & Jerry’s “Big” Day – Part 2")]
    [InlineData("Tom &amp; Jerry&#39;s Day", "Tom & Jerry's Day")]
    [InlineData("Tom &amp; Jerry&#39;s Day", "Tom &amp; Jerry&#39;s Day")]
    [InlineData("Tom & Jerry’s Day — Part 2", "Tom & Jerry's Day - Part 2")]
    public void EpisodeTitleToken_TypographicAndEntityVariants_Match(string title, string titleInDescription)
    {
        Extract(titleInDescription + " " + Advisory, StripTitlePattern, title).Should().Be(Advisory);
    }

    [Theory(DisplayName = "EpisodeTitleToken_EmptyTitle_NeverMatches")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EpisodeTitleToken_EmptyTitle_NeverMatches(string? title)
    {
        const string description = "Some description";
        Extract(description, StripTitlePattern, title).Should().Be(description);
        // an empty title must not turn the token into an empty (match-everything) group
        Extract(description, @"^{episode-title}(?<body>.*)$", title).Should().Be(description);
    }

    [Fact(DisplayName = "PodcastNameToken_StripsLeadingPodcastName")]
    public void PodcastNameToken_StripsLeadingPodcastName()
    {
        var description = PodcastName + " presents: " + Advisory;
        Extract(description, @"^\s*{podcast-name}\s*presents:\s*(?<body>.*)$").Should().Be(Advisory);
    }

    [Fact(DisplayName = "PodcastNameAndEpisodeTitleTokens_CanBeCombined")]
    public void PodcastNameAndEpisodeTitleTokens_CanBeCombined()
    {
        var description = PodcastName + " - " + Title + " " + Advisory;
        Extract(description, @"^\s*{podcast-name}\s*-\s*{episode-title}\s*(?<body>.*)$").Should().Be(Advisory);
    }

    [Fact(DisplayName = "Token_RepeatedInPattern_AllOccurrencesAreExpanded")]
    public void Token_RepeatedInPattern_AllOccurrencesAreExpanded()
    {
        var description = Title + " " + Title + " " + Advisory;
        Extract(description, @"^\s*{episode-title}\s*{episode-title}\s*(?<body>.*)$").Should().Be(Advisory);
    }

    [Fact(DisplayName = "Token_FollowedByQuantifier_AppliesToWholeTitle")]
    public void Token_FollowedByQuantifier_AppliesToWholeTitle()
    {
        Extract(Advisory, @"^\s*(?:{episode-title}\s*)?(?<body>.*)$").Should().Be(Advisory);
        Extract(Title + " " + Advisory, @"^\s*(?:{episode-title}\s*)?(?<body>.*)$").Should().Be(Advisory);
        Extract(Title + " " + Advisory, @"^\s*{episode-title}?\s*(?<body>.*)$").Should().Be(Advisory);
    }

    [Fact(DisplayName = "UnknownToken_IsLeftUntouched")]
    public void UnknownToken_IsLeftUntouched()
    {
        DescriptionRegexTokens.Expand(@"^{episode_title}{podcastName}{Episode-Title}{unknown}(?<body>.*)$", Title,
                PodcastName)
            .Should().Be(@"^{episode_title}{podcastName}{Episode-Title}{unknown}(?<body>.*)$");
        Extract("{unknown} text", @"^{unknown}\s*(?<body>.*)$").Should().Be("text");
    }

    [Fact(DisplayName = "TokenValue_ContainingAnotherToken_IsNotReExpanded")]
    public void TokenValue_ContainingAnotherToken_IsNotReExpanded()
    {
        var expanded = DescriptionRegexTokens.Expand("{episode-title}", "{podcast-name}", "Never");
        expanded.Should().NotContain("Never");
        Extract("{podcast-name} body", @"^{episode-title}\s*(?<body>.*)$", "{podcast-name}", "Never")
            .Should().Be("body");
    }

    [Theory(DisplayName = "TokenFreePattern_BehavesExactlyAsBefore")]
    [InlineData(@"^(?<body>.*?)\s*Patreon.*$", "Great show. Patreon: link", "Great show.")]
    [InlineData(@"^.*(?'body')$", "Anything at all", "")]
    [InlineData(@"^\s*(?:#[\w-]+\s*)+(?'body'.*)\z", "#one #two Real text", "Real text")]
    [InlineData(@"^[^\r\n]*\r?\n\s*(?'body'.*)\z", "Advert\r\nReal text", "Real text")]
    [InlineData(@"^x{2}(?<body>.*)$", "xxrest", "rest")]
    [InlineData(@"^never(?<body>.*)$", "no match here", "no match here")]
    public void TokenFreePattern_BehavesExactlyAsBefore(string pattern, string description, string expected)
    {
        Extract(description, pattern).Should().Be(expected);
        Sut.ExtractDescription(description, pattern).Should().Be(expected);
        Sut.ExtractDescription(description, new Regex(pattern, Podcast.DescriptionFlags)).Should().Be(expected);
        DescriptionRegexTokens.Expand(pattern, Title, PodcastName).Should().Be(pattern);
    }

    [Fact(DisplayName = "ExtractDescription_NoPattern_ReturnsDescriptionUnchanged")]
    public void ExtractDescription_NoPattern_ReturnsDescriptionUnchanged()
    {
        const string description = "<b>raw</b> description";
        Sut.ExtractDescription(description, string.Empty, Title, PodcastName).Should().Be(description);
        Sut.ExtractDescription(description, "  ", Title, PodcastName).Should().Be(description);
    }

    [Fact(DisplayName = "ExtractDescription_TokenPatternWithoutContext_LeavesDescriptionUnchanged")]
    public void ExtractDescription_TokenPatternWithoutContext_LeavesDescriptionUnchanged()
    {
        Sut.ExtractDescription(Title + " " + Advisory, StripTitlePattern)
            .Should().Be(Title + " " + Advisory);
    }

    [Fact(DisplayName = "ExtractDescription_HtmlDescription_TitleStrippedAfterTagsRemoved")]
    public void ExtractDescription_HtmlDescription_TitleStrippedAfterTagsRemoved()
    {
        Extract("<p><b>" + Title + "</b></p><p>" + Advisory + "</p>", StripTitlePattern).Should().Be(Advisory);
    }

    [Fact(DisplayName = "Create_NoPattern_ReturnsNull")]
    public void Create_NoPattern_ReturnsNull()
    {
        DescriptionRegexTokens.Create(null, Title, PodcastName).Should().BeNull();
        DescriptionRegexTokens.Create(" ", Title, PodcastName).Should().BeNull();
    }

    [Fact(DisplayName = "Create_UsesDescriptionFlags")]
    public void Create_UsesDescriptionFlags()
    {
        DescriptionRegexTokens.Create(StripTitlePattern, Title, PodcastName)!.Options
            .Should().Be(Podcast.DescriptionFlags);
    }

    [Theory(DisplayName = "ContainsTokens_RecognisesOnlyTheExactTokens")]
    [InlineData("{episode-title}", true)]
    [InlineData("^{podcast-name}$", true)]
    [InlineData("{Episode-Title}", false)]
    [InlineData("{episode_title}", false)]
    [InlineData("^(?<body>.*)$", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsTokens_RecognisesOnlyTheExactTokens(string? pattern, bool expected)
    {
        DescriptionRegexTokens.ContainsTokens(pattern).Should().Be(expected);
    }

    [Fact(DisplayName = "PostModel_DescriptionRegexWithEpisodeTitleToken_StripsFirstEpisodeTitle")]
    public void PostModel_DescriptionRegexWithEpisodeTitleToken_StripsFirstEpisodeTitle()
    {
        var episode = new EpisodePost(
            Title.Replace("&", "&amp;"), null, null, null, "1 Jan 2026", "[0:30:00]",
            Title + Advisory, "Spotify-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), [], null, null);
        var postModel = new PostModel(PodcastName, string.Empty, StripTitlePattern, [episode], null, [], []);

        Sut.SanitiseDescription(postModel).Should().Be(AdvisoryAfterFixCharacters);
    }

    [Fact(DisplayName = "PostModel_DescriptionRegexWithPodcastNameToken_UsesPodcastName")]
    public void PostModel_DescriptionRegexWithPodcastNameToken_UsesPodcastName()
    {
        var episode = new EpisodePost(
            "Title", null, null, null, "1 Jan 2026", "[0:30:00]",
            PodcastName + " " + Advisory, "Spotify-1", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), [], null,
            null);
        var postModel = new PostModel(PodcastName, string.Empty, @"^{podcast-name}\s*(?<body>.*)$", [episode], null,
            [], []);

        Sut.SanitiseDescription(postModel).Should().Be(AdvisoryAfterFixCharacters);
    }
}