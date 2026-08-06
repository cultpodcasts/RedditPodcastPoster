using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests;

public class TextSanitiserLanguageLowerCaseTermsTests
{
    private readonly AutoMocker _mocker = new();

    public TextSanitiserLanguageLowerCaseTermsTests()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    [Theory(DisplayName =
        "English/null language: mid-title headline small words are lowered after title-case.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("en")]
    [InlineData("en-GB")]
    [InlineData("en_US")]
    public async Task SanitiseTitle_WithEnglishLanguage_LowersMidTitleSmallWords(string? language)
    {
        // Arrange
        const string input = "Secrets Of The Group";
        const string expected = "Secrets of the Group";

        // Act
        var result = await Sut.SanitiseTitle(input, null, [], [], language);

        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "Non-English language with empty rules: do not apply English headline small-word lowering.")]
    [InlineData("pl")]
    [InlineData("fr")]
    [InlineData("es-ES")]
    public async Task SanitiseTitle_WithNonEnglishLanguage_DoesNotLowerEnglishSmallWords(
        string language)
    {
        // Arrange
        const string input = "Secrets Of The Group";
        const string expected = "Secrets Of The Group";

        // Act
        var result = await Sut.SanitiseTitle(input, null, [], [], language);

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName =
        "Non-English language with configured lower-case terms: apply that language's list.")]
    public async Task SanitiseTitle_WithConfiguredNonEnglishLowerTerms_AppliesThoseTerms()
    {
        // Arrange
        var mocker = new AutoMocker();
        TitleCasingTestSupport.UseRules(
            mocker,
            TitleCasingTestSupport.CreateEnglishDefault(),
            new LanguageTitleCasingRulesDocument("pl")
            {
                LowerCaseTerms = ["of", "the"],
                KnownTerms = []
            });
        var sut = mocker.CreateInstance<TextSanitiser>();

        // Act
        var result = await sut.SanitiseTitle("Secrets Of The Group", null, [], [], "pl");

        // Assert
        result.Should().Be("Secrets of the Group");
    }

    [Theory(DisplayName =
        "Mc/Mac casing is applied regardless of language (Mc always; Mac when allowlisted).")]
    [InlineData(null)]
    [InlineData("pl")]
    public async Task SanitiseTitle_WithMcOrAllowlistedMac_RecapitalisesRegardlessOfLanguage(
        string? language)
    {
        // Arrange
        const string input = "Interview With McTerm And MacEwan";
        var expectedEnglish = "Interview with McTerm And MacEwan";
        var expectedNonEnglish = "Interview With McTerm And MacEwan";
        var expected = LowerCaseTerms.IsEnglish(language) ? expectedEnglish : expectedNonEnglish;

        // Act
        var result = await Sut.SanitiseTitle(input, null, [], [], language);

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName =
        "Known term literal may include a space and replaces regex matches for that language.")]
    public async Task SanitiseTitle_WithSpacedKnownTerm_ReplacesWithLiteral()
    {
        // Arrange
        var mocker = new AutoMocker();
        TitleCasingTestSupport.UseRules(
            mocker,
            TitleCasingTestSupport.CreateEnglishDefault(
                lowerCaseTerms: [],
                knownTerms:
                [
                    new KnownTermEntry
                    {
                        Literal = "BBC Radio",
                        Pattern = @"\bbbc radio\b",
                        Options = "IgnoreCase, Compiled"
                    }
                ]));
        var sut = mocker.CreateInstance<TextSanitiser>();

        // Act
        var result = await sut.SanitiseTitle("Listen To Bbc Radio Tonight", null, [], [], "en");

        // Assert
        result.Should().Be("Listen To BBC Radio Tonight");
    }
}
