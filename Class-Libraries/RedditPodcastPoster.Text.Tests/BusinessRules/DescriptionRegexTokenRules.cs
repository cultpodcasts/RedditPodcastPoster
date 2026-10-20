using System.Text.RegularExpressions;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

/// <summary>
/// Rules for podcast description regexes containing the <c>{episode-title}</c> / <c>{podcast-name}</c> tokens
/// (<see cref="DescriptionRegexTokens" />), applied through <see cref="TextSanitiser" />.
/// See docs/description-regex-tokens.md.
/// </summary>
public class DescriptionRegexTokenRules
{
    // The website "Episode title prefix removal" description-regex preset.
    private const string TitlePrefixPreset = "^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$";

    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public DescriptionRegexTokenRules()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    private static readonly Fixture DataFixture = new();

    private string Word() => "w" + _fixture.Create<Guid>().ToString("N")[..8];

    private static string DataWord() => "w" + DataFixture.Create<Guid>().ToString("N")[..8];

    // Generated titles around an ampersand.
    public static TheoryData<string> AmpersandTitles() => new() { $"{DataWord()} & {DataWord()}" };

    // Generated (title, title as it appears in the description) pairs exercising apostrophe, quote, dash and
    // ampersand equivalence (typographic and HTML-entity forms).
    public static TheoryData<string, string> TypographicAndEntityVariants()
    {
        var (a, b, c, d, e) = (DataWord(), DataWord(), DataWord(), DataWord(), DataWord());
        return new TheoryData<string, string>
        {
            { $"{a} & {b}'s \"{c}\" {d} - {e} 2", $"{a} &amp; {b}&#39;s &quot;{c}&quot; {d} &ndash; {e} 2" },
            { $"{a} & {b}'s \"{c}\" {d} - {e} 2", $"{a} & {b}\u2019s \u201C{c}\u201D {d} \u2013 {e} 2" },
            { $"{a} &amp; {b}&#39;s {d}", $"{a} & {b}'s {d}" },
            { $"{a} &amp; {b}&#39;s {d}", $"{a} &amp; {b}&#39;s {d}" },
            { $"{a} & {b}\u2019s {d} \u2014 {e} 2", $"{a} & {b}'s {d} - {e} 2" }
        };
    }

    // Scenario shape: an episode title containing a colon and an em dash.
    private string CreateTitle() => $"{Word()} {Word()}: {Word()} {Word()} \u2014 {Word()} {Word()}";

    private string CreatePodcastName() => $"{Word()}-{Word()} & {Word()}";

    private string CreateBody() => $"{Word().ToUpperInvariant()}: {Word()} {Word()}. {Word()} {Word()}.";

    [Fact(DisplayName =
        "When the description starts with the episode title and runs straight into the body with no separator, " +
        "the title-prefix preset strips the title and keeps the whole body.")]
    public void title_run_into_body_without_separator_is_stripped()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription(title + body, TitlePrefixPreset, title, CreatePodcastName());

        // Assert
        result.Should().Be(body);
    }

    [Theory(DisplayName =
        "When the episode title is followed by a dash, en dash, em dash, colon, pipe, line breaks, spaces or nothing, " +
        "the title-prefix preset strips the title and the separator.")]
    [InlineData(" - ")]
    [InlineData(" \u2013 ")]
    [InlineData(" \u2014 ")]
    [InlineData(": ")]
    [InlineData(" | ")]
    [InlineData("\r\n\r\n")]
    [InlineData("   ")]
    [InlineData("")]
    public void title_and_separator_are_stripped(string separator)
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription("  " + title + separator + body, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "When an episode's description does not start with its title, the description is left unchanged, " +
        "so the title-prefix preset is safe for every episode of the podcast.")]
    public void description_without_title_prefix_is_unchanged()
    {
        // Arrange
        var title = CreateTitle();
        var description = CreateBody();

        // Act
        var result = Sut.ExtractDescription(description, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(description);
    }

    [Fact(DisplayName =
        "When the episode title appears only part-way through the description, the description is left unchanged, " +
        "because the preset is anchored to the start.")]
    public void title_in_middle_of_description_is_unchanged()
    {
        // Arrange
        var title = CreateTitle();
        var description = Word() + ". " + title + " " + CreateBody();

        // Act
        var result = Sut.ExtractDescription(description, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(description);
    }

    [Fact(DisplayName = "When the title in the description differs only in letter case, it is still stripped.")]
    public void title_match_is_case_insensitive()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription(title.ToUpperInvariant() + " " + body, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "When the body after the title spans several lines, all of it is kept, because token patterns are " +
        "compiled with Singleline.")]
    public void multiline_body_is_kept()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody() + "\r\n" + CreateBody() + "\r\n" + CreateBody();

        // Act
        var result = Sut.ExtractDescription(title + "\r\n" + body, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "When the description has different whitespace (line breaks, repeated spaces) where the title has single " +
        "spaces, the title still matches.")]
    public void whitespace_runs_in_description_still_match()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();
        var titleInDescription = title.Replace(" ", "  ").Replace(": ", ":\r\n");

        // Act
        var result = Sut.ExtractDescription(titleInDescription + " " + body, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "When the episode title has leading, trailing or repeated whitespace, it is trimmed and each whitespace run " +
        "matches any whitespace run.")]
    public void whitespace_in_title_is_normalised()
    {
        // Arrange
        var first = Word();
        var second = Word();
        var body = CreateBody();

        // Act
        var expanded = DescriptionRegexTokens.Expand(DescriptionRegexTokens.EpisodeTitle, $"  {first} \t {second} \n",
            null);
        var result = Sut.ExtractDescription($"{first} {second} {body}", TitlePrefixPreset,
            $"  {first} \t {second} \n", null);

        // Assert
        expanded.Should().Be($@"(?:{first}\s+{second})");
        result.Should().Be(body);
    }

    [Theory(DisplayName =
        "When the episode title contains regex metacharacters or special characters, they are escaped and matched " +
        "literally.")]
    [InlineData("What? (Part 1) [Live] * + . $ ^ | \\ {x} #1")]
    [MemberData(nameof(AmpersandTitles), DisableDiscoveryEnumeration = true)]
    [InlineData("Why (and how)?")]
    [InlineData("Part 1 \u2014 The Beginning")]
    [InlineData("C++ $100 ^ [x] {y} a|b")]
    [InlineData("(?:evil)|x")]
    [InlineData(".*")]
    public void special_characters_in_title_match_literally(string title)
    {
        // Arrange
        var body = CreateBody();

        // Act
        var stripped = Sut.ExtractDescription(title + " " + body, TitlePrefixPreset, title, null);
        var other = Sut.ExtractDescription("Something else " + body, TitlePrefixPreset, title, null);

        // Assert
        stripped.Should().Be(body);
        other.Should().Be("Something else " + body);
    }

    [Fact(DisplayName =
        "When the title contains a regex metacharacter such as '.', it does not match a different character in the " +
        "description.")]
    public void metacharacter_in_title_does_not_match_other_text()
    {
        // Arrange
        var description = "axb " + CreateBody();

        // Act
        var result = Sut.ExtractDescription(description, TitlePrefixPreset, "a.b", null);

        // Assert
        result.Should().Be(description);
    }

    [Fact(DisplayName = "When the title looks like a named group, it cannot inject capturing groups into the regex.")]
    public void title_cannot_inject_capturing_groups()
    {
        // Arrange
        const string title = "(?<body>evil)";

        // Act
        var regex = DescriptionRegexTokens.Create(TitlePrefixPreset, title, null)!;

        // Assert
        regex.GetGroupNames().Should().BeEquivalentTo("0", "body");
    }

    [Theory(DisplayName =
        "When the title and the description use different apostrophe, quote, dash or ampersand forms (typographic " +
        "or HTML entities), the title still matches.")]
    [MemberData(nameof(TypographicAndEntityVariants), DisableDiscoveryEnumeration = true)]
    public void typographic_and_entity_variants_match(string title, string titleInDescription)
    {
        // Arrange
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription(titleInDescription + " " + body, TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(body);
    }

    [Theory(DisplayName =
        "When the episode title is missing or blank, the token never matches and the description is left unchanged, " +
        "rather than the token becoming an empty match-anything group.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void blank_title_never_matches(string? title)
    {
        // Arrange
        var description = CreateBody();

        // Act
        var preset = Sut.ExtractDescription(description, TitlePrefixPreset, title, null);
        var bare = Sut.ExtractDescription(description, @"^{episode-title}(?<body>.*)$", title, null);

        // Assert
        preset.Should().Be(description);
        bare.Should().Be(description);
    }

    [Fact(DisplayName = "When the pattern uses {podcast-name}, it resolves to the podcast name.")]
    public void podcast_name_token_resolves_to_podcast_name()
    {
        // Arrange
        var podcastName = CreatePodcastName();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription(podcastName + " presents: " + body,
            @"^\s*{podcast-name}\s*presents:\s*(?<body>.*)$", CreateTitle(), podcastName);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName = "When a pattern combines {podcast-name} and {episode-title}, both resolve.")]
    public void podcast_name_and_episode_title_combine()
    {
        // Arrange
        var podcastName = CreatePodcastName();
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription($"{podcastName} - {title} {body}",
            @"^\s*{podcast-name}\s*-\s*{episode-title}\s*(?<body>.*)$", title, podcastName);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName = "When a token appears more than once in a pattern, every occurrence is resolved.")]
    public void repeated_token_is_resolved_everywhere()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription($"{title} {title} {body}",
            @"^\s*{episode-title}\s*{episode-title}\s*(?<body>.*)$", title, null);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "When a quantifier follows a token, it applies to the whole value, because the value is a non-capturing " +
        "group.")]
    public void quantifier_after_token_applies_to_whole_value()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var withoutTitle = Sut.ExtractDescription(body, @"^\s*{episode-title}?\s*(?<body>.*)$", title, null);
        var withTitle = Sut.ExtractDescription(title + " " + body, @"^\s*{episode-title}?\s*(?<body>.*)$", title,
            null);

        // Assert
        withoutTitle.Should().Be(body);
        withTitle.Should().Be(body);
    }

    [Theory(DisplayName =
        "When a pattern contains brace text that is not an exact recognised token, or a regex quantifier such as " +
        "{2}, {2,3} or {2,}, it is left exactly as written.")]
    [InlineData(@"^{episode_title}{podcastName}{Episode-Title}{unknown}(?<body>.*)$")]
    [InlineData(@"^x{2}(?<body>.*)$")]
    [InlineData(@"^x{2,3}(?<body>.*)$")]
    [InlineData(@"^x{2,}(?<body>.*)$")]
    public void non_token_braces_are_untouched(string pattern)
    {
        // Arrange
        var title = CreateTitle();

        // Act
        var expanded = DescriptionRegexTokens.Expand(pattern, title, CreatePodcastName());

        // Assert
        expanded.Should().Be(pattern);
        DescriptionRegexTokens.ContainsTokens(pattern).Should().BeFalse();
    }

    [Theory(DisplayName =
        "When a pattern mixes regex quantifiers with a token, only the token is replaced and the quantifiers still " +
        "work.")]
    [InlineData("12. ", true)]
    [InlineData("1234. ", false)]
    public void quantifier_and_token_both_work(string prefix, bool stripped)
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();
        var description = prefix + title + " " + body;
        const string pattern = @"^\d{1,3}\.\s*{episode-title}\s*(?<body>.*)$";

        // Act
        var result = Sut.ExtractDescription(description, pattern, title, null);

        // Assert
        DescriptionRegexTokens.Expand(pattern, "T", null).Should().Be(@"^\d{1,3}\.\s*(?:T)\s*(?<body>.*)$");
        result.Should().Be(stripped ? body : description);
    }

    [Fact(DisplayName =
        "When a token's opening brace is escaped (\\{episode-title}), it is literal text, not a token.")]
    public void escaped_brace_is_literal()
    {
        // Arrange
        const string pattern = @"^\{episode-title}\s*(?<body>.*)$";
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription("{episode-title} " + body, pattern, CreateTitle(), null);

        // Assert
        DescriptionRegexTokens.ContainsTokens(pattern).Should().BeFalse();
        DescriptionRegexTokens.Expand(pattern, "T", null).Should().Be(pattern);
        result.Should().Be(body);
    }

    [Theory(DisplayName =
        "When a token is preceded by an escaped backslash it is still a token, but an odd number of backslashes " +
        "escapes the brace.")]
    [InlineData(@"^\\{episode-title}(?<body>.*)$", @"^\\(?:T)(?<body>.*)$")]
    [InlineData(@"^\\\{episode-title}(?<body>.*)$", @"^\\\{episode-title}(?<body>.*)$")]
    public void backslashes_before_token(string pattern, string expected)
    {
        // Arrange
        const string title = "T";

        // Act
        var expanded = DescriptionRegexTokens.Expand(pattern, title, null);

        // Assert
        expanded.Should().Be(expected);
    }

    [Fact(DisplayName =
        "When a token value itself contains token text, it is not expanded again, because tokens are replaced in a " +
        "single pass.")]
    public void token_value_containing_token_text_is_not_re_expanded()
    {
        // Arrange
        var podcastName = CreatePodcastName();
        var body = CreateBody();

        // Act
        var expanded = DescriptionRegexTokens.Expand(DescriptionRegexTokens.EpisodeTitle,
            DescriptionRegexTokens.PodcastName, podcastName);
        var result = Sut.ExtractDescription("{podcast-name} " + body, @"^{episode-title}\s*(?<body>.*)$",
            DescriptionRegexTokens.PodcastName, podcastName);

        // Assert
        expanded.Should().NotContain(podcastName);
        result.Should().Be(body);
    }

    [Theory(DisplayName =
        "When a pattern has no tokens, it behaves exactly as before: the pattern is compiled as written and gives " +
        "the same result as the regex built directly.")]
    [InlineData(@"^(?<body>.*?)\s*Patreon.*$", "Great show. Patreon: link", "Great show.")]
    [InlineData(@"^.*(?'body')$", "Anything at all", "")]
    [InlineData(@"^\s*(?:#[\w-]+\s*)+(?'body'.*)\z", "#one #two Real text", "Real text")]
    [InlineData(@"^[^\r\n]*\r?\n\s*(?'body'.*)\z", "Advert\r\nReal text", "Real text")]
    [InlineData(@"^x{2}(?<body>.*)$", "xxrest", "rest")]
    [InlineData(@"^never(?<body>.*)$", "no match here", "no match here")]
    public void token_free_pattern_is_unchanged(string pattern, string description, string expected)
    {
        // Arrange
        var direct = new Regex(pattern, Podcast.DescriptionFlags);

        // Act
        var viaTokens = Sut.ExtractDescription(description, pattern, CreateTitle(), CreatePodcastName());
        var viaRegex = Sut.ExtractDescription(description, direct);

        // Assert
        viaTokens.Should().Be(expected);
        viaRegex.Should().Be(expected);
        DescriptionRegexTokens.Create(pattern, CreateTitle(), null)!.ToString().Should().Be(pattern);
    }

    [Theory(DisplayName = "When the pattern is blank, the description is returned unchanged.")]
    [InlineData("")]
    [InlineData("  ")]
    public void blank_pattern_returns_description_unchanged(string pattern)
    {
        // Arrange
        var description = "<b>" + CreateBody() + "</b>";

        // Act
        var result = Sut.ExtractDescription(description, pattern, CreateTitle(), CreatePodcastName());

        // Assert
        result.Should().Be(description);
    }

    [Fact(DisplayName =
        "When the description is HTML, the title is stripped after the tags are removed.")]
    public void html_description_title_stripped_after_tags_removed()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();

        // Act
        var result = Sut.ExtractDescription($"<p><b>{title}</b></p><p>{body}</p>", TitlePrefixPreset, title, null);

        // Assert
        result.Should().Be(body);
    }

    [Theory(DisplayName = "When there is no pattern, no regex is created.")]
    [InlineData(null)]
    [InlineData(" ")]
    public void no_pattern_creates_no_regex(string? pattern)
    {
        // Arrange
        var title = CreateTitle();

        // Act
        var regex = DescriptionRegexTokens.Create(pattern, title, CreatePodcastName());

        // Assert
        regex.Should().BeNull();
    }

    [Fact(DisplayName =
        "When a pattern has tokens, it is always compiled with Podcast.DescriptionFlags, whatever options a caller " +
        "uses for token-free patterns.")]
    public void token_pattern_uses_description_flags()
    {
        // Arrange
        var title = CreateTitle();

        // Act
        var byDefault = DescriptionRegexTokens.Create(TitlePrefixPreset, title, null)!;
        var withNone = DescriptionRegexTokens.Create(TitlePrefixPreset, title, null, RegexOptions.None)!;

        // Assert
        byDefault.Options.Should().Be(Podcast.DescriptionFlags);
        withNone.Options.Should().Be(Podcast.DescriptionFlags);
    }

    [Fact(DisplayName =
        "When a token-free pattern is created with the caller's historic options (the YouTube enricher uses none), " +
        "those options are kept and the result is identical to the regex the caller built before.")]
    public void token_free_pattern_keeps_caller_options()
    {
        // Arrange
        const string pattern = @"^[^\r\n]*\r?\n\s*(?'body'.*)\z";
        var description = Word() + "\n" + Word() + "\n" + Word();
        var before = new Regex(pattern);

        // Act
        var after = DescriptionRegexTokens.Create(pattern, CreateTitle(), null, RegexOptions.None)!;

        // Assert
        after.Options.Should().Be(RegexOptions.None);
        after.ToString().Should().Be(pattern);
        Sut.ExtractDescription(description, after).Should().Be(Sut.ExtractDescription(description, before));
    }

    [Fact(DisplayName =
        "When a token-free pattern is created without caller options, it is compiled as written with " +
        "Podcast.DescriptionFlags.")]
    public void token_free_pattern_default_options()
    {
        // Arrange
        const string pattern = @"^.*(?'body')$";

        // Act
        var regex = DescriptionRegexTokens.Create(pattern, CreateTitle(), null)!;

        // Assert
        regex.ToString().Should().Be(pattern);
        regex.Options.Should().Be(Podcast.DescriptionFlags);
    }

    [Theory(DisplayName = "Only the exact, lower-case tokens are recognised.")]
    [InlineData("{episode-title}", true)]
    [InlineData("^{podcast-name}$", true)]
    [InlineData("{Episode-Title}", false)]
    [InlineData("{episode_title}", false)]
    [InlineData("^(?<body>.*)$", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void only_exact_tokens_are_recognised(string? pattern, bool expected)
    {
        // Arrange
        var input = pattern;

        // Act
        var result = DescriptionRegexTokens.ContainsTokens(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "When validating, patterns containing tokens are accepted (tokens are expanded with sample values first), " +
        "and blank patterns are accepted because they clear the regex.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$")]
    [InlineData(@"^{podcast-name}\s*(?<body>.*)$")]
    [InlineData(@"^\s*(?:#[\w-]+\s*)+(?'body'.*)\z")]
    [InlineData(@"^x{2,3}(?<body>.*)$")]
    public void valid_patterns_pass_validation(string? pattern)
    {
        // Arrange
        var input = pattern;

        // Act
        var valid = DescriptionRegexTokens.TryValidate(input, out var error);

        // Assert
        valid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory(DisplayName = "When validating, an invalid regex is rejected with an error, with or without tokens.")]
    [InlineData(@"^(?<body>.*$")]
    [InlineData(@"^{episode-title}(?<body>.*$")]
    [InlineData(@"[z-a]")]
    public void invalid_patterns_fail_validation(string pattern)
    {
        // Arrange
        var input = pattern;

        // Act
        var valid = DescriptionRegexTokens.TryValidate(input, out var error);

        // Assert
        valid.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "Every description regex is created with a match timeout, so a pathological curator pattern cannot hang " +
        "a publish, indexer run or API request.")]
    public void created_regexes_have_match_timeout()
    {
        // Arrange
        var title = CreateTitle();

        // Act
        var tokenRegex = DescriptionRegexTokens.Create(TitlePrefixPreset, title, null)!;
        var tokenFreeRegex = DescriptionRegexTokens.Create(@"^x(?<body>.*)$", title, null, RegexOptions.None)!;

        // Assert
        tokenRegex.MatchTimeout.Should().Be(DescriptionRegexTokens.MatchTimeout);
        tokenFreeRegex.MatchTimeout.Should().Be(DescriptionRegexTokens.MatchTimeout);
    }

    [Fact(DisplayName =
        "When a description regex match times out, the description is left unchanged instead of the caller failing, " +
        "and a warning naming the pattern is logged so the pathological podcast regex can be found.")]
    public void timed_out_match_leaves_description_unchanged()
    {
        // Arrange
        var description = new string('x', 40);
        var pathological = new Regex(@"^(?<body>(x+x+)+y)$", RegexOptions.None, TimeSpan.FromMilliseconds(1));

        // Act
        var result = Sut.ExtractDescription(description, pathological);

        // Assert
        result.Should().Be(description);
        _mocker.GetMock<ILogger<TextSanitiser>>().Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(pathological.ToString())),
                It.Is<Exception?>(ex => ex is RegexMatchTimeoutException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When a post model's podcast description regex uses {episode-title}, the first episode's title is stripped " +
        "from the post description.")]
    public void post_model_episode_title_token_strips_title()
    {
        // Arrange
        var title = CreateTitle();
        var body = CreateBody();
        var episode = CreateEpisodePost(title.Replace("&", "&amp;"), title + body);
        var postModel = new PostModel(CreatePodcastName(), string.Empty, TitlePrefixPreset, [episode], null, [], []);

        // Act
        var result = Sut.SanitiseDescription(postModel);

        // Assert
        result.Should().Be(body);
    }

    [Fact(DisplayName =
        "When a post model's podcast description regex uses {podcast-name}, the podcast name is stripped from the " +
        "post description.")]
    public void post_model_podcast_name_token_strips_podcast_name()
    {
        // Arrange
        var podcastName = CreatePodcastName();
        var body = CreateBody();
        var episode = CreateEpisodePost(CreateTitle(), podcastName + " " + body);
        var postModel = new PostModel(podcastName, string.Empty, @"^{podcast-name}\s*(?<body>.*)$", [episode], null,
            [], []);

        // Act
        var result = Sut.SanitiseDescription(postModel);

        // Assert
        result.Should().Be(body);
    }

    private EpisodePost CreateEpisodePost(string title, string description)
    {
        return new EpisodePost(
            title, null, null, null, _fixture.Create<string>(), _fixture.Create<string>(), description,
            _fixture.Create<string>(), _fixture.Create<DateTime>(), [], null, null);
    }
}
