using AutoFixture;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

/// <summary>
/// A parent's title regex (podcast, news organisation or TV show) is built only by
/// <see cref="Podcast.CreateTitleRegex"/> and matches case-insensitively, so an outlet suffix is stripped whatever
/// casing the stored title (or an earlier step) gave it (#1378).
/// </summary>
public class ParentTitleRegexCaseRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly string _outlet;
    private readonly string _pattern;

    public ParentTitleRegexCaseRules()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
        _outlet = "SYNTH" + _fixture.Create<int>().ToString("D6");
        _pattern = $@"^(?'title'.*?)(?'suffix' \| {_outlet})?$";
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    [Fact(DisplayName =
        "Plain English rule: when a parent's title regex strips an upper-case outlet suffix, then a title carrying " +
        "that suffix in any casing is shown without it, because the display regex is case-insensitive (#1378).")]
    public async Task outlet_suffix_is_stripped_whatever_its_casing()
    {
        // Arrange
        var headline = "synthetic headline " + _fixture.Create<int>();
        var regex = Podcast.CreateTitleRegex(_pattern);
        string[] titles =
        [
            $"{headline} | {_outlet}",
            $"{headline} | {_outlet.ToLowerInvariant()}",
            $"{headline} | {_outlet[..1]}{_outlet[1..].ToLowerInvariant()}"
        ];

        // Act
        var results = new List<string>();
        foreach (var title in titles)
        {
            results.Add(await Sut.SanitiseTitle(title, regex, [], []));
        }

        // Assert
        results.Should().AllSatisfy(result =>
        {
            result.Should().NotContain("|");
            result.Should().NotContainEquivalentOf(_outlet);
            result.Should().BeEquivalentTo(headline);
        });
    }

    [Fact(DisplayName =
        "Plain English rule: when a parent has no title regex, then no display regex is built and the outlet suffix " +
        "stays in the title, because only a curator's indexing.titleRegex strips text.")]
    public async Task missing_title_regex_leaves_suffix()
    {
        // Arrange
        var title = $"synthetic headline {_fixture.Create<int>()} | {_outlet}";

        // Act
        var regex = Podcast.CreateTitleRegex("  ");
        var result = await Sut.SanitiseTitle(title, regex, [], []);

        // Assert
        regex.Should().BeNull();
        result.Should().ContainEquivalentOf(_outlet);
    }
}
