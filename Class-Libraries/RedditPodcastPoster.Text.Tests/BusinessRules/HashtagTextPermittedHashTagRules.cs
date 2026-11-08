using FluentAssertions;
using RedditPodcastPoster.Text.HashTags;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class HashtagTextPermittedHashTagRules
{
    [Theory(DisplayName =
        "A HashTag is permitted on both X and Bluesky only when its body is letters, digits, combining marks, and underscore — not ampersand, plus, dash, apostrophe, or other punctuation.")]
    [InlineData("Cult", true)]
    [InlineData("#Cult", true)]
    [InlineData("Señor", true)]
    [InlineData("Cult_Watch", true)]
    [InlineData("1Cult", true)]
    [InlineData("AlphaBeta", true)]
    [InlineData("Q&A", false)]
    [InlineData("C++", false)]
    [InlineData("LGBTQ+", false)]
    [InlineData("Some-Tag", false)]
    [InlineData("Term's", false)]
    [InlineData("Term.A", false)]
    [InlineData("Alpha Beta", false)]
    [InlineData("#123", false)]
    [InlineData("", false)]
    public void is_permitted_hash_tag_matches_x_and_bluesky_intersection(string stored, bool expected)
    {
        // Arrange / Act
        var permitted = HashtagText.IsPermittedHashTag(stored);

        // Assert
        permitted.Should().Be(expected);
        HashtagText.IsReproducedAsIs(stored).Should().Be(expected);
    }

    [Theory(DisplayName =
        "Enrichment hashtag compaction removes whitespace only and permits the result when that body is a valid X and Bluesky hashtag; symbols such as dash, ampersand, and plus are not stripped to invent a different tag.")]
    [InlineData("Alpha Beta", "AlphaBeta")]
    [InlineData("Term One", "TermOne")]
    [InlineData("#Cult", "Cult")]
    [InlineData("Some-Tag", null)]
    [InlineData("Q&A", null)]
    [InlineData("C++", null)]
    [InlineData("Term's A", null)]
    [InlineData("Term. A", null)]
    public void enrichment_body_compacts_whitespace_only_when_permitted(string enrichmentTag, string? expected)
    {
        // Arrange / Act
        var body = HashtagText.ToEnrichmentHashtagBody(enrichmentTag);

        // Assert
        body.Should().Be(expected);
    }

    [Theory(DisplayName =
        "Stored HashTag fields keep only space-delimited tokens that are permitted platform hashtags, because invalid tokens must not be persisted.")]
    [InlineData("#Cult #Q&A #Alpha", "#Cult #Alpha")]
    [InlineData("Q&A C++", null)]
    [InlineData("  #Cult  ", "#Cult")]
    [InlineData("", null)]
    public void filter_permitted_stored_hash_tag_field_drops_invalid_tokens(string input, string? expected)
    {
        // Arrange / Act
        var filtered = HashtagText.FilterPermittedStoredHashTagField(input);

        // Assert
        filtered.Should().Be(expected);
    }

    [Theory(DisplayName =
        "Enrichment HashTag arrays keep only phrases whose whitespace-compacted body is a permitted platform hashtag.")]
    [InlineData(new[] { "Alpha Beta", "Q&A", "Cult" }, new[] { "Alpha Beta", "Cult" })]
    [InlineData(new[] { "Some-Tag", "C++" }, null)]
    public void filter_permitted_enrichment_hash_tags_drops_invalid_phrases(string[] input, string[]? expected)
    {
        // Arrange / Act
        var filtered = HashtagText.FilterPermittedEnrichmentHashTags(input);

        // Assert
        filtered.Should().Equal(expected);
    }
}
