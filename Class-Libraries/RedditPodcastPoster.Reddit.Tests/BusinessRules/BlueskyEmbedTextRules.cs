using System.Globalization;
using FluentAssertions;
using RedditPodcastPoster.Bluesky.Client;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// Embed card title/description caps (#1228): truncation must keep whole text elements so a cut never
/// splits a ZWJ emoji sequence, skin-tone modifier, or combining mark.
/// </summary>
public class BlueskyEmbedTextRules
{
    [Fact(DisplayName =
        "When an ASCII title is longer than the embed cap, the result is exactly the cap length and ends with an ellipsis, because External.Properties still needs a bounded string.")]
    public void ascii_over_the_cap_is_truncated_to_the_cap_length()
    {
        // Arrange
        var title = new string('a', BlueskyEmbedText.MaxTitleLength + 8);

        // Act
        var truncated = BlueskyEmbedText.Truncate(title, BlueskyEmbedText.MaxTitleLength);

        // Assert
        truncated.Length.Should().Be(BlueskyEmbedText.MaxTitleLength);
        truncated.Should().EndWith("…");
        truncated[..^1].Should().Be(new string('a', BlueskyEmbedText.MaxTitleLength - 1));
    }

    [Fact(DisplayName =
        "When the cut lands inside a ZWJ family emoji, the whole sequence is dropped before the ellipsis, because a half emoji is not valid card text.")]
    public void truncate_does_not_split_a_zwj_emoji_sequence()
    {
        // Arrange — family emoji is one text element; prefix length places the UTF-16 cut inside it.
        const string zwjFamily = "\U0001F468\u200D\U0001F469\u200D\U0001F467";
        new StringInfo(zwjFamily).LengthInTextElements.Should().Be(1);
        zwjFamily.Length.Should().BeGreaterThan(2);
        var prefix = new string('a', BlueskyEmbedText.MaxTitleLength - 1 - (zwjFamily.Length / 2));
        var title = prefix + zwjFamily;
        title.Length.Should().BeGreaterThan(BlueskyEmbedText.MaxTitleLength);
        (BlueskyEmbedText.MaxTitleLength - 1).Should().BeInRange(prefix.Length + 1, prefix.Length + zwjFamily.Length - 1);

        // Act
        var truncated = BlueskyEmbedText.Truncate(title, BlueskyEmbedText.MaxTitleLength);

        // Assert
        truncated.Should().Be(prefix + "…");
        truncated.Should().NotContain("\u200D");
        truncated.Should().NotContain("\U0001F468");
    }

    [Fact(DisplayName =
        "When the cut lands inside a skin-tone-modified emoji, the base and modifier are dropped together, because a lone modifier is not valid card text.")]
    public void truncate_does_not_split_a_skin_tone_modifier()
    {
        // Arrange — thumbs-up + medium skin tone is one text element; cut lands inside the pair.
        const string skinToneEmoji = "\U0001F44D\U0001F3FD";
        new StringInfo(skinToneEmoji).LengthInTextElements.Should().Be(1);
        skinToneEmoji.Length.Should().Be(4);
        var prefix = new string('a', BlueskyEmbedText.MaxTitleLength - 1 - 2);
        var title = prefix + skinToneEmoji;
        (BlueskyEmbedText.MaxTitleLength - 1).Should().BeInRange(prefix.Length + 1, prefix.Length + skinToneEmoji.Length - 1);

        // Act
        var truncated = BlueskyEmbedText.Truncate(title, BlueskyEmbedText.MaxTitleLength);

        // Assert
        truncated.Should().Be(prefix + "…");
        truncated.Should().NotContain("\U0001F44D");
        truncated.Should().NotContain("\U0001F3FD");
    }

    [Fact(DisplayName =
        "When the cut lands inside a base letter plus combining marks, the whole cluster is dropped, because a bare combining mark after ASCII is not valid card text.")]
    public void truncate_does_not_split_a_combining_mark_cluster()
    {
        // Arrange — one grapheme of base 'e' plus many combining acutes straddles the embed cut.
        var combiningCluster = "e" + new string('\u0301', 20);
        new StringInfo(combiningCluster).LengthInTextElements.Should().Be(1);
        combiningCluster.Length.Should().Be(21);
        var prefix = new string('a', BlueskyEmbedText.MaxTitleLength - 1 - 10);
        var title = prefix + combiningCluster;
        (BlueskyEmbedText.MaxTitleLength - 1).Should().BeInRange(prefix.Length + 1, prefix.Length + combiningCluster.Length - 1);

        // Act
        var truncated = BlueskyEmbedText.Truncate(title, BlueskyEmbedText.MaxTitleLength);

        // Assert
        truncated.Should().Be(prefix + "…");
        truncated.Should().NotContain("\u0301");
        truncated.Should().NotEndWith("e…");
    }

    [Fact(DisplayName =
        "When a description cut lands inside a ZWJ emoji sequence, the whole sequence is dropped before the ellipsis, because description uses the same whole-text-element helper as the title.")]
    public void description_truncate_does_not_split_a_zwj_emoji_sequence()
    {
        // Arrange
        const string zwjFamily = "\U0001F468\u200D\U0001F469\u200D\U0001F467";
        var prefix = new string('a', BlueskyEmbedText.MaxDescriptionLength - 1 - (zwjFamily.Length / 2));
        var description = prefix + zwjFamily;
        (BlueskyEmbedText.MaxDescriptionLength - 1)
            .Should().BeInRange(prefix.Length + 1, prefix.Length + zwjFamily.Length - 1);

        // Act
        var truncated = BlueskyEmbedText.Truncate(description, BlueskyEmbedText.MaxDescriptionLength);

        // Assert
        truncated.Should().Be(prefix + "…");
        truncated.Should().NotContain("\u200D");
    }
}
