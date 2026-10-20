using System.Text.RegularExpressions;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests;

/// <summary>
/// Validation, quantifier-brace and option-preservation rules for <see cref="DescriptionRegexTokens" />.
/// </summary>
public class DescriptionRegexTokensValidationTests
{
    // The exact example that motivated the tokens: the description begins with the title and continues
    // straight into the body with no separator.
    private const string CultimatumPodcast = "Cultimatum-The Culture of Cults & Human Trafficking";

    private const string CultimatumTitle =
        "Cheryl Hope: Trafficked at Age 6 \u2014 Breaking the Silence on Exclusive Brethren";

    private const string CultimatumBody =
        "CONTENT ADVISORY: This episode discusses child abuse.\nCheryl Hope was born into the Exclusive Brethren.";

    // The pattern of the website "Episode title prefix removal" preset.
    private const string PresetPattern = "^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$";

    private readonly AutoMocker _mocker = new();

    public DescriptionRegexTokensValidationTests()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    [Fact(DisplayName = "Cultimatum example: the preset strips the leading episode title and keeps the whole body")]
    public void Cultimatum_PresetStripsTitle()
    {
        Sut.ExtractDescription(CultimatumTitle + CultimatumBody, PresetPattern, CultimatumTitle, CultimatumPodcast)
            .Should().Be(CultimatumBody);
    }

    [Fact(DisplayName = "Cultimatum example: does not depend on a CONTENT ADVISORY literal")]
    public void Cultimatum_NoAdvisory_StillStripsTitle()
    {
        const string body = "Cheryl Hope was born into the Exclusive Brethren.";
        Sut.ExtractDescription(CultimatumTitle + " " + body, PresetPattern, CultimatumTitle, CultimatumPodcast)
            .Should().Be(body);
    }

    [Fact(DisplayName = "Cultimatum example: another episode of the podcast without the title prefix is unchanged")]
    public void Cultimatum_OtherEpisodeWithoutPrefix_Unchanged()
    {
        const string description = "In this episode we talk to a survivor.";
        Sut.ExtractDescription(description, PresetPattern, CultimatumTitle, CultimatumPodcast)
            .Should().Be(description);
    }

    [Theory(DisplayName = "Regex quantifier braces are never treated as tokens")]
    [InlineData(@"^x{2}(?<body>.*)$")]
    [InlineData(@"^x{2,3}(?<body>.*)$")]
    [InlineData(@"^x{2,}(?<body>.*)$")]
    [InlineData(@"^\d{1,3}\.\s*{episode-title}(?<body>.*)$")]
    [InlineData(@"^{episode-title}{2}(?<body>.*)$")]
    public void QuantifierBraces_AreUntouched(string pattern)
    {
        // Only the exact token is replaced; every quantifier brace survives verbatim.
        DescriptionRegexTokens.Expand(pattern, "T", "P")
            .Should().Be(pattern.Replace(DescriptionRegexTokens.EpisodeTitle, "(?:T)"));
    }

    [Fact(DisplayName = "An escaped opening brace (\\{episode-title}) is a literal, not a token")]
    public void EscapedBrace_IsNotAToken()
    {
        DescriptionRegexTokens.ContainsTokens(@"^\{episode-title}(?<body>.*)$").Should().BeFalse();
        DescriptionRegexTokens.Expand(@"^\{episode-title}(?<body>.*)$", "T", "P")
            .Should().Be(@"^\{episode-title}(?<body>.*)$");
        Sut.ExtractDescription("{episode-title} rest", @"^\{episode-title}\s*(?<body>.*)$", "T", null)
            .Should().Be("rest");
    }

    [Fact(DisplayName = "An escaped backslash before a token (\\\\{episode-title}) keeps the token")]
    public void EscapedBackslashBeforeToken_IsAToken()
    {
        DescriptionRegexTokens.Expand(@"^\\{episode-title}(?<body>.*)$", "T", "P")
            .Should().Be(@"^\\(?:T)(?<body>.*)$");
        DescriptionRegexTokens.Expand(@"^\\\{episode-title}(?<body>.*)$", "T", "P")
            .Should().Be(@"^\\\{episode-title}(?<body>.*)$");
    }

    [Fact(DisplayName = "A quantifier with a range is applied as a quantifier alongside a token")]
    public void QuantifierAndToken_BothWork()
    {
        Sut.ExtractDescription("12. My Title rest", @"^\d{1,3}\.\s*{episode-title}\s*(?<body>.*)$", "My Title", null)
            .Should().Be("rest");
        Sut.ExtractDescription("1234. My Title rest", @"^\d{1,3}\.\s*{episode-title}\s*(?<body>.*)$", "My Title",
                null)
            .Should().Be("1234. My Title rest");
    }

    [Theory(DisplayName = "Special characters in the title are escaped and matched literally")]
    [InlineData("Tom & Jerry")]
    [InlineData("Why (and how)?")]
    [InlineData("Part 1 \u2014 The Beginning")]
    [InlineData("C++ $100 ^ [x] {y} a|b \\ #tag")]
    public void SpecialCharactersInTitle_AreEscaped(string title)
    {
        Sut.ExtractDescription(title + " body", PresetPattern, title, null).Should().Be("body");
        // the escaped title must not match a different string of the same shape
        Sut.ExtractDescription("Something else body", PresetPattern, title, null).Should().Be("Something else body");
    }

    [Fact(DisplayName = "Whitespace in the title matches any whitespace run in the description")]
    public void TitleWhitespace_IsFlexible()
    {
        DescriptionRegexTokens.Expand("{episode-title}", "a b", null).Should().Be(@"(?:a\s+b)");
    }

    [Theory(DisplayName = "TryValidate accepts patterns containing tokens and blank patterns")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$")]
    [InlineData(@"^{podcast-name}\s*(?<body>.*)$")]
    [InlineData(@"^\s*(?:#[\w-]+\s*)+(?'body'.*)\z")]
    [InlineData(@"^x{2,3}(?<body>.*)$")]
    public void TryValidate_Valid(string? pattern)
    {
        DescriptionRegexTokens.TryValidate(pattern, out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory(DisplayName = "TryValidate rejects invalid regexes, with or without tokens")]
    [InlineData(@"^(?<body>.*$")]
    [InlineData(@"^{episode-title}(?<body>.*$")]
    [InlineData(@"[z-a]")]
    public void TryValidate_Invalid(string pattern)
    {
        DescriptionRegexTokens.TryValidate(pattern, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Token-free pattern with tokenFreeOptions keeps the caller's historic options")]
    public void Create_TokenFree_UsesTokenFreeOptions()
    {
        const string pattern = @"^[^\r\n]*\r?\n\s*(?'body'.*)\z";
        var regex = DescriptionRegexTokens.Create(pattern, "Title", "Podcast", RegexOptions.None)!;
        regex.Options.Should().Be(RegexOptions.None);
        regex.ToString().Should().Be(pattern);
    }

    [Fact(DisplayName = "Token pattern always uses Podcast.DescriptionFlags, whatever tokenFreeOptions is")]
    public void Create_Token_UsesDescriptionFlags()
    {
        DescriptionRegexTokens.Create(PresetPattern, "Title", "Podcast", RegexOptions.None)!.Options
            .Should().Be(Podcast.DescriptionFlags);
    }

    [Fact(DisplayName = "Token-free pattern is compiled exactly as written with DescriptionFlags by default")]
    public void Create_TokenFree_Default()
    {
        const string pattern = @"^.*(?'body')$";
        var regex = DescriptionRegexTokens.Create(pattern, "Title", "Podcast")!;
        regex.ToString().Should().Be(pattern);
        regex.Options.Should().Be(Podcast.DescriptionFlags);
    }

    [Fact(DisplayName = "YouTube enricher semantics: a multi-line token-free pattern behaves as before with no options")]
    public void TokenFree_NoOptions_SameAsNewRegex()
    {
        const string pattern = @"^[^\r\n]*\r?\n\s*(?'body'.*)\z";
        const string description = "Advert\nLine one\nLine two";
        var before = new Regex(pattern);
        var after = DescriptionRegexTokens.Create(pattern, "Title", "Podcast", RegexOptions.None)!;
        Sut.ExtractDescription(description, after).Should().Be(Sut.ExtractDescription(description, before));
    }
}