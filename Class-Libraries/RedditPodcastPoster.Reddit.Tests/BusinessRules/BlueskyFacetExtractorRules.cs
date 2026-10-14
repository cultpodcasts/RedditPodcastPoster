using System.Text;
using FluentAssertions;
using idunno.AtProto;
using idunno.Bluesky.RichText;
using RedditPodcastPoster.Bluesky.RichText;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyFacetExtractorRules
{
    private const string TruncatedHandle = "alice.smith";
    private const string FullHandle = "alice.smith-jones.bsky.social";
    private const string FullToken = "@" + FullHandle;
    private const string TruncatedAccountDid = "did:plc:aaaaaaaaaaaaaaaaaaaaaa";
    private const string FullAccountDid = "did:plc:bbbbbbbbbbbbbbbbbbbbbb";

    [Fact(DisplayName =
        "A dashed @mention that overlaps a shorter resolved mention uses one facet for the full handle, because the default extractor stops at the hyphen and would otherwise mention a different account.")]
    public async Task overlapping_truncated_mention_is_replaced_by_the_full_handle()
    {
        // Arrange
        const string text = "é " + FullToken;
        var charIndex = text.IndexOf(FullToken, StringComparison.Ordinal);
        var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, charIndex));
        var byteEnd = byteStart + Encoding.UTF8.GetByteCount(FullToken);
        var resolved = new List<string>();
        var sut = new BlueskyFacetExtractor((handle, _) =>
        {
            resolved.Add(handle);
            Did account = handle == FullHandle ? FullAccountDid : TruncatedAccountDid;
            return Task.FromResult<Did?>(account);
        });

        // Act
        var facets = await sut.ExtractFacets(text);

        // Assert
        byteStart.Should().NotBe(charIndex);
        resolved.Should().Contain(TruncatedHandle);
        resolved.Should().Contain(FullHandle);
        var facet = facets.Should().ContainSingle().Subject;
        facet.Index.ByteStart.Should().Be(byteStart);
        facet.Index.ByteEnd.Should().Be(byteEnd);
        MentionDid(facet).Value.Should().Be(FullAccountDid);
    }

    [Fact(DisplayName =
        "When a dashed @mention overlaps a shorter resolved mention and the full handle has no account, the post does not mention the truncated account.")]
    public async Task unresolved_full_handle_drops_the_truncated_mention()
    {
        // Arrange
        var sut = new BlueskyFacetExtractor((handle, _) =>
        {
            if (handle == FullHandle)
            {
                return Task.FromResult<Did?>(null);
            }

            Did truncatedAccount = TruncatedAccountDid;
            return Task.FromResult<Did?>(truncatedAccount);
        });

        // Act
        var facets = await sut.ExtractFacets(FullToken);

        // Assert
        facets.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A fault while resolving a dashed handle fails facet extraction, because the post must not succeed after losing that mention.")]
    public async Task resolve_fault_propagates()
    {
        // Arrange
        var sut = new BlueskyFacetExtractor((handle, _) =>
        {
            if (handle == FullHandle)
            {
                throw new InvalidOperationException("resolve failed");
            }

            Did truncatedAccount = TruncatedAccountDid;
            return Task.FromResult<Did?>(truncatedAccount);
        });

        // Act
        var act = () => sut.ExtractFacets(FullToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName =
        "Cancelling dashed-handle resolution stops facet extraction, because a cancelled post must not continue.")]
    public async Task cancellation_propagates()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var sut = new BlueskyFacetExtractor((handle, _) =>
        {
            if (handle == FullHandle)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }

            Did truncatedAccount = TruncatedAccountDid;
            return Task.FromResult<Did?>(truncatedAccount);
        });

        // Act
        var act = () => sut.ExtractFacets(FullToken, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Did MentionDid(Facet facet)
    {
        return facet.Features.OfType<MentionFacetFeature>().Single().Did;
    }

    [Fact(DisplayName =
        "Each hashtag gets exactly one tag facet, whether or not the default extractor found it, so a tag after whitespace is not duplicated and one after a quote is not lost.")]
    public async Task hashtags_get_one_facet_each()
    {
        // Arrange
        var sut = new BlueskyFacetExtractor((_, _) => Task.FromResult<Did?>(null));

        // Act
        var facets = await sut.ExtractFacets("\"#OpusDei, x\" (#Sect) #Cult, #Cult");

        // Assert
        facets.SelectMany(f => f.Features).OfType<TagFacetFeature>().Select(t => t.Tag)
            .Should().Equal("OpusDei", "Sect", "Cult", "Cult");
        facets.Select(f => f.Index.ByteStart).Should().BeInAscendingOrder();
    }

    [Fact(DisplayName =
        "A '#' fragment inside a link stays part of the link facet and gets no tag facet.")]
    public async Task url_fragment_is_not_a_tag()
    {
        // Arrange
        var sut = new BlueskyFacetExtractor((_, _) => Task.FromResult<Did?>(null));

        // Act
        var facets = await sut.ExtractFacets("see https://example.com/page#section now");

        // Assert
        facets.SelectMany(f => f.Features).OfType<TagFacetFeature>().Should().BeEmpty();
        facets.SelectMany(f => f.Features).OfType<LinkFacetFeature>().Should().ContainSingle();
    }
}
