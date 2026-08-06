using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Tests;

public class TextSanitiserMcMacPrefixTests
{
    private readonly AutoMocker _mocker = new();

    public TextSanitiserMcMacPrefixTests()
    {
        TitleCasingTestSupport.UseDefaultEnglishRules(_mocker);
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    [Theory(DisplayName =
        "Mc surnames: after title-case, keep Mc and capitalise the following letter.")]
    [InlineData("Blah McTerm Blah", "Blah McTerm Blah")]
    [InlineData("Blah mcterm Blah", "Blah McTerm Blah")]
    [InlineData("Blah MCTERM Blah", "Blah McTerm Blah")]
    [InlineData("McTerm Alone", "McTerm Alone")]
    public async Task SanitiseTitle_WithMcSurname_RecapitalisesLetterAfterPrefix(
        string input, string expected)
    {
        // Arrange
        // Act
        var result = await Sut.SanitiseTitle(input, null, [], []);
        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "Mac surnames on the allowlist: after title-case, keep Mac and capitalise the following letter.")]
    [InlineData("Blah MacEwan Blah", "Blah MacEwan Blah")]
    [InlineData("Blah macewan Blah", "Blah MacEwan Blah")]
    [InlineData("Blah MACEWAN Blah", "Blah MacEwan Blah")]
    [InlineData("MacDonald Alone", "MacDonald Alone")]
    [InlineData("macdonald Alone", "MacDonald Alone")]
    [InlineData("MacKenzie Topic", "MacKenzie Topic")]
    public async Task SanitiseTitle_WithAllowlistedMacSurname_RecapitalisesLetterAfterPrefix(
        string input, string expected)
    {
        // Arrange
        // Act
        var result = await Sut.SanitiseTitle(input, null, [], []);
        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "Mac tokens not on the surname allowlist: leave title-case as-is (no MacHine / MacHination).")]
    [InlineData("The Machine Topic")]
    [InlineData("Machination Topic")]
    [InlineData("Macro Economics")]
    [InlineData("Macron Topic")]
    [InlineData("Machiavelli Topic")]
    [InlineData("Unknown Macterm Topic")]
    public async Task SanitiseTitle_WithNonAllowlistedMacWord_DoesNotRecapitaliseAfterMac(
        string expected)
    {
        // Arrange
        // Act
        var result = await Sut.SanitiseTitle(expected, null, [], []);
        // Assert
        result.Should().Be(expected);
    }
}
