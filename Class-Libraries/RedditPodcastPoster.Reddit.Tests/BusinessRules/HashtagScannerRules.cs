using System.Text;
using FluentAssertions;
using RedditPodcastPoster.Bluesky.RichText;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class HashtagScannerRules
{
    [Theory(DisplayName =
        "A hashtag links at the start of text, after whitespace, or after opening quotes and brackets, and trailing punctuation is not part of the tag.")]
    [InlineData("#OpusDei", "OpusDei")]
    [InlineData("\"#OpusDei, Tangara & Redfield\"", "OpusDei")]
    [InlineData("say \"#OpusDei\"", "OpusDei")]
    [InlineData("'#Cult'", "Cult")]
    [InlineData("(#Cult)", "Cult")]
    [InlineData("[#Cult]", "Cult")]
    [InlineData("{#Cult}", "Cult")]
    [InlineData("“#Cult”", "Cult")]
    [InlineData("‘#Cult’", "Cult")]
    [InlineData("«#Cult»", "Cult")]
    [InlineData("(\"#Cult\")", "Cult")]
    [InlineData("a #Cult, b", "Cult")]
    [InlineData("a #Cult. b", "Cult")]
    [InlineData("a #Cult!?", "Cult")]
    [InlineData("a #Cult…", "Cult")]
    [InlineData("a ＃Cult", "Cult")]
    [InlineData("a #1Cult", "1Cult")]
    [InlineData("a #Cult's", "Cult's")]
    [InlineData("a #Señor", "Señor")]
    [InlineData("a #カルト", "カルト")]
    [InlineData("line\n#Cult", "Cult")]
    public void hashtag_is_found(string text, string expectedTag)
    {
        HashtagScanner.Find(text).Should().ContainSingle().Which.Tag.Should().Be(expectedTag);
    }

    [Theory(DisplayName =
        "A '#' inside a word, a URL, after other punctuation, or followed only by digits or punctuation is not a hashtag.")]
    [InlineData("Opus#Dei")]
    [InlineData("https://example.com/#section")]
    [InlineData("Let's#Cult")]
    [InlineData("a,#Cult")]
    [InlineData("a #2026")]
    [InlineData("a #")]
    [InlineData("a #,")]
    [InlineData("a #\uFE0F")]
    [InlineData("a ##")]
    public void hashtag_is_not_found(string text)
    {
        HashtagScanner.Find(text).Should().BeEmpty();
    }

    [Fact(DisplayName = "A tag of 64 characters links and one of 65 does not, matching Bluesky's limit.")]
    public void tag_length_limit()
    {
        HashtagScanner.Find("#" + new string('a', HashtagScanner.MaxTagLength)).Should().ContainSingle();
        HashtagScanner.Find("#" + new string('a', HashtagScanner.MaxTagLength + 1)).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Hashtag ranges are UTF-8 byte offsets that cover the '#' and exclude trailing punctuation, so multi-byte text before or inside the tag is counted in bytes.")]
    public void ranges_are_utf8_byte_offsets()
    {
        // Arrange
        const string text = "é “#Señor”, then #Cult.";

        // Act
        var tokens = HashtagScanner.Find(text);

        // Assert
        tokens.Should().HaveCount(2);
        AssertSpan(text, tokens[0], "#Señor");
        AssertSpan(text, tokens[1], "#Cult");
    }

    [Fact(DisplayName = "Several hashtags in one post are each found once, in order.")]
    public void several_hashtags()
    {
        HashtagScanner.Find("\"#OpusDei, x\"\n#Catholicism #HumanTrafficking #Cult")
            .Select(t => t.Tag)
            .Should().Equal("OpusDei", "Catholicism", "HumanTrafficking", "Cult");
    }

    private static void AssertSpan(string text, HashtagToken token, string expected)
    {
        var charIndex = text.IndexOf(expected, StringComparison.Ordinal);
        var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));
        byteStart.Should().NotBe(charIndex);
        token.ByteStart.Should().Be(byteStart);
        token.ByteEnd.Should().Be(byteStart + Encoding.UTF8.GetByteCount(expected));
    }
}
