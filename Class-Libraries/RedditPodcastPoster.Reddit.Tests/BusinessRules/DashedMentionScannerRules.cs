using System.Text;
using FluentAssertions;
using RedditPodcastPoster.Bluesky.RichText;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class DashedMentionScannerRules
{
    [Fact(DisplayName =
        "A Bluesky @mention whose handle contains a dash is detected, and its range is a UTF-8 byte span so a character index would be wrong.")]
    public void dashed_handle_mention_uses_utf8_byte_range()
    {
        // Arrange
        const string mention = "@some-name.bsky.social";
        const string text = "é " + mention;
        var charIndex = text.IndexOf(mention, StringComparison.Ordinal);
        var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));

        // Act
        var matches = DashedMentionScanner.Find(text);

        // Assert
        byteStart.Should().NotBe(charIndex);
        var match = matches.Should().ContainSingle().Subject;
        match.Handle.Should().Be("some-name.bsky.social");
        match.ByteStart.Should().Be(byteStart);
        match.ByteEnd.Should().Be(byteStart + Encoding.UTF8.GetByteCount(mention));
    }

    [Fact(DisplayName =
        "A Bluesky @mention whose handle has no dash is not claimed by the dashed-handle pass, because the default extractor owns those mentions.")]
    public void handle_without_dash_is_not_claimed_by_dashed_pass()
    {
        // Arrange
        const string text = "é @plain.bsky.social";

        // Act
        var matches = DashedMentionScanner.Find(text);

        // Assert
        matches.Should().BeEmpty();
    }
}
