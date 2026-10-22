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
    [InlineData("a #Cult) b", "Cult")]
    [InlineData("a #Cult_Watch", "Cult_Watch")]
    [InlineData("a #Cult_", "Cult")]
    [InlineData("a #Café", "Café")]
    [InlineData("a #Cafe\u0301", "Cafe\u0301")]
    [InlineData("a #Señor", "Señor")]
    [InlineData("a #カルト", "カルト")]
    [InlineData("a #カルトー", "カルトー")]
    [InlineData("line\n#Cult", "Cult")]
    public void hashtag_is_found(string text, string expectedTag)
    {
        // Arrange
        // (text and expected tag come from the inline data)

        // Act
        var tokens = HashtagScanner.Find(text);

        // Assert
        tokens.Should().ContainSingle().Which.Tag.Should().Be(expectedTag);
    }

    [Theory(DisplayName =
        "A hashtag ends at the first character that is not a letter, digit, combining mark or underscore, so a dash, apostrophe, quote, punctuation or emoji straight after an inserted tag is not linked as part of it.")]
    [InlineData("Escaped a #Cult—and spoke up", "Cult")]
    [InlineData("Escaped a #Cult–and spoke up", "Cult")]
    [InlineData("Escaped a #Cult-and spoke up", "Cult")]
    [InlineData("Escaped a #Cult's grip", "Cult")]
    [InlineData("Escaped a #Cult’s grip", "Cult")]
    [InlineData("Escaped a #Cult\u02BCs grip", "Cult")]
    [InlineData("Escaped a #Cult\"s grip", "Cult")]
    [InlineData("Escaped a #Cult”s grip", "Cult")]
    [InlineData("Escaped a #Cult,and spoke up", "Cult")]
    [InlineData("Escaped a #Cult.And spoke up", "Cult")]
    [InlineData("Escaped a #Cult:and spoke up", "Cult")]
    [InlineData("Escaped a #Cult/Sect", "Cult")]
    [InlineData("Escaped a #Cult🙏and spoke up", "Cult")]
    [InlineData("Escaped a #Cult\u200Dand spoke up", "Cult")]
    [InlineData("Escaped a #Cult\uFE0Fand spoke up", "Cult")]
    public void hashtag_ends_at_first_non_word_character(string text, string expectedTag)
    {
        // Arrange
        var expected = "#" + expectedTag;

        // Act
        var tokens = HashtagScanner.Find(text);

        // Assert
        tokens.Should().ContainSingle().Which.Tag.Should().Be(expectedTag);
        var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, text.IndexOf('#')));
        tokens[0].ByteStart.Should().Be(byteStart);
        tokens[0].ByteEnd.Should().Be(byteStart + Encoding.UTF8.GetByteCount(expected));
    }

    [Fact(DisplayName =
        "A hashtag followed by an em dash covers only the tag's bytes, and the byte range stays right when multi-byte characters come before the tag and the 3-byte em dash comes after it.")]
    public void em_dash_after_tag_with_multibyte_before()
    {
        // Arrange
        const string text = "“Café 🙏 a #Cult—and #Sect–then”";

        // Act
        var tokens = HashtagScanner.Find(text);

        // Assert
        tokens.Select(t => t.Tag).Should().Equal("Cult", "Sect");
        AssertSpan(text, tokens[0], "#Cult");
        AssertSpan(text, tokens[1], "#Sect");
        var bytes = Encoding.UTF8.GetBytes(text);
        Encoding.UTF8.GetString(bytes, (int)tokens[0].ByteStart, (int)(tokens[0].ByteEnd - tokens[0].ByteStart))
            .Should().Be("#Cult");
        Encoding.UTF8.GetString(bytes, (int)tokens[0].ByteEnd, 3).Should().Be("—");
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
    [InlineData("a #—and")]
    [InlineData("a #_")]
    public void hashtag_is_not_found(string text)
    {
        // Arrange
        // (text comes from the inline data)

        // Act
        var tokens = HashtagScanner.Find(text);

        // Assert
        tokens.Should().BeEmpty();
    }

    [Fact(DisplayName = "A tag of 64 characters links and one of 65 does not, matching Bluesky's limit.")]
    public void tag_length_limit()
    {
        // Arrange
        var longest = "#" + new string('a', HashtagScanner.MaxTagLength);
        var tooLong = "#" + new string('a', HashtagScanner.MaxTagLength + 1);

        // Act
        var longestTokens = HashtagScanner.Find(longest);
        var tooLongTokens = HashtagScanner.Find(tooLong);

        // Assert
        longestTokens.Should().ContainSingle();
        tooLongTokens.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "A stored hashtag is turned into tag text by dropping the leading '#' and every character a tag can't hold, so the scanner links the whole written tag.")]
    [InlineData("#Cult", "Cult")]
    [InlineData("Cult", "Cult")]
    [InlineData("＃Cult", "Cult")]
    [InlineData("#Some-Tag", "SomeTag")]
    [InlineData("#Jehovah’sWitnesses", "JehovahsWitnesses")]
    [InlineData("#Q&A", "QA")]
    [InlineData("#LGBTQ+", "LGBTQ")]
    [InlineData("#Cult_Watch_", "Cult_Watch")]
    [InlineData("#Señor", "Señor")]
    [InlineData("#Cafe\u0301", "Cafe\u0301")]
    [InlineData("#カルト", "カルト")]
    [InlineData("#Cult🙏", "Cult")]
    [InlineData("#1Cult", "1Cult")]
    public void stored_hashtag_becomes_linkable_tag_text(string stored, string expected)
    {
        // Arrange
        // (stored and expected come from the inline data)

        // Act
        var tagText = HashtagScanner.ToTagText(stored);

        // Assert
        tagText.Should().Be(expected);
        HashtagScanner.Find("#" + tagText).Should().ContainSingle().Which.Tag.Should().Be(expected);
    }

    [Theory(DisplayName =
        "A stored hashtag with nothing linkable left (empty, digits or '_' only, or emoji or dashes only) has no tag text, so it is not written.")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#123")]
    [InlineData("#_")]
    [InlineData("#🙏")]
    [InlineData("#-—")]
    public void stored_hashtag_with_nothing_linkable_has_no_tag_text(string? stored)
    {
        // Arrange
        // (stored comes from the inline data)

        // Act
        var tagText = HashtagScanner.ToTagText(stored);

        // Assert
        tagText.Should().BeNull();
    }

    [Fact(DisplayName = "A stored hashtag longer than 64 characters has no tag text, matching the scanner's limit.")]
    public void stored_hashtag_over_length_limit_has_no_tag_text()
    {
        // Arrange
        var stored = "#" + new string('a', HashtagScanner.MaxTagLength + 1);

        // Act
        var tagText = HashtagScanner.ToTagText(stored);

        // Assert
        tagText.Should().BeNull();
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
        // Arrange
        const string text = "\"#OpusDei, x\"\n#Catholicism #HumanTrafficking #Cult";

        // Act
        var tokens = HashtagScanner.Find(text);

        // Assert
        tokens.Select(t => t.Tag).Should().Equal("OpusDei", "Catholicism", "HumanTrafficking", "Cult");
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
