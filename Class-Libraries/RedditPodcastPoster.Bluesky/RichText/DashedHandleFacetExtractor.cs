using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace RedditPodcastPoster.Bluesky.RichText;

public sealed class DashedHandleFacetExtractor : IFacetExtractor
{
    private readonly DefaultFacetExtractor _defaultExtractor;
    private readonly Func<string, CancellationToken, Task<Did?>> _resolveHandle;

    public DashedHandleFacetExtractor(Func<string, CancellationToken, Task<Did?>> resolveHandle)
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
            if (IsCovered(facets, mention.ByteStart, mention.ByteEnd))
            {
                continue;
            }

            Did? did;
            try
            {
                did = await _resolveHandle(mention.Handle, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                continue;
            }

            if (did is null)
            {
                continue;
            }

            IList<FacetFeature> features = [new MentionFacetFeature(did)];
            facets.Add(new Facet(new ByteSlice(mention.ByteStart, mention.ByteEnd), features));
        }

        return facets;
    }

    private static bool IsCovered(IReadOnlyList<Facet> facets, long byteStart, long byteEnd)
    {
        foreach (var facet in facets)
        {
            if (facet.Index is null)
            {
                continue;
            }

            if (facet.Index.ByteStart < byteEnd && byteStart < facet.Index.ByteEnd)
            {
                return true;
            }
        }

        return false;
    }
}
