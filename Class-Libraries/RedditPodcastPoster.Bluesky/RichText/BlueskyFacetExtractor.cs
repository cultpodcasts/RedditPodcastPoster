using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace RedditPodcastPoster.Bluesky.RichText;

/// <summary>
/// Wraps idunno's default extractor. Mentions with dashed handles get the full handle, and hashtags
/// follow <see cref="HashtagScanner"/> (Bluesky's rule, plus tags after opening quotes or brackets)
/// instead of the default tag pattern, which only accepts '#' after whitespace.
/// </summary>
public sealed class BlueskyFacetExtractor : IFacetExtractor
{
    private readonly DefaultFacetExtractor _defaultExtractor;
    private readonly Func<string, CancellationToken, Task<Did?>> _resolveHandle;

    public BlueskyFacetExtractor(Func<string, CancellationToken, Task<Did?>> resolveHandle)
    {
        ArgumentNullException.ThrowIfNull(resolveHandle);
        _resolveHandle = resolveHandle;
        _defaultExtractor = new DefaultFacetExtractor(resolveHandle);
    }

    public async Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default)
    {
        var facets = new List<Facet>(
            await _defaultExtractor.ExtractFacets(text, cancellationToken).ConfigureAwait(false));

        foreach (var mention in DashedMentionScanner.Find(text))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsFullyCovered(facets, mention.ByteStart, mention.ByteEnd))
            {
                continue;
            }

            // The default mention pattern stops at '-'. A shorter mention facet that
            // overlaps this token is that truncation, and it can name a different account.
            RemoveTruncatedMentionFacets(facets, mention.ByteStart, mention.ByteEnd);

            Did? did;
            try
            {
                did = await _resolveHandle(mention.Handle, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (did is null)
            {
                continue;
            }

            IList<FacetFeature> features = [new MentionFacetFeature(did)];
            facets.Add(new Facet(new ByteSlice(mention.ByteStart, mention.ByteEnd), features));
        }

        // One hashtag rule: replace the default tag facets with the scanner's.
        facets.RemoveAll(HasTag);
        foreach (var hashtag in HashtagScanner.Find(text))
        {
            if (Overlaps(facets, hashtag.ByteStart, hashtag.ByteEnd))
            {
                continue;
            }

            IList<FacetFeature> features = [new TagFacetFeature(hashtag.Tag)];
            facets.Add(new Facet(new ByteSlice(hashtag.ByteStart, hashtag.ByteEnd), features));
        }

        facets.Sort((a, b) => (a.Index?.ByteStart ?? 0).CompareTo(b.Index?.ByteStart ?? 0));
        return facets;
    }

    private static bool Overlaps(IReadOnlyList<Facet> facets, long byteStart, long byteEnd)
    {
        foreach (var facet in facets)
        {
            if (facet.Index is not null && facet.Index.ByteStart < byteEnd && byteStart < facet.Index.ByteEnd)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTag(Facet facet) => facet.Features.Any(feature => feature is TagFacetFeature);

    private static bool IsFullyCovered(IReadOnlyList<Facet> facets, long byteStart, long byteEnd)
    {
        foreach (var facet in facets)
        {
            if (facet.Index is not null &&
                facet.Index.ByteStart <= byteStart &&
                facet.Index.ByteEnd >= byteEnd)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveTruncatedMentionFacets(List<Facet> facets, long byteStart, long byteEnd)
    {
        facets.RemoveAll(facet => IsTruncatedMention(facet, byteStart, byteEnd));
    }

    private static bool IsTruncatedMention(Facet facet, long byteStart, long byteEnd)
    {
        if (facet.Index is null || !HasMention(facet))
        {
            return false;
        }

        var overlaps = facet.Index.ByteStart < byteEnd && byteStart < facet.Index.ByteEnd;
        var coversFullToken = facet.Index.ByteStart <= byteStart && facet.Index.ByteEnd >= byteEnd;
        return overlaps && !coversFullToken;
    }

    private static bool HasMention(Facet facet)
    {
        foreach (var feature in facet.Features)
        {
            if (feature is MentionFacetFeature)
            {
                return true;
            }
        }

        return false;
    }
}
