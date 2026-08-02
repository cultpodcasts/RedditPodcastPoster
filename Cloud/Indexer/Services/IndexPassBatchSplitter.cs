namespace Indexer.Services;

/// <summary>
/// Splits indexable podcast ids into contiguous hourly pass batches (same algorithm as
/// <c>IndexIdProvider</c>). Every id lands in exactly one batch so schedule coverage of
/// passes 1–4 is enough to cover the full catalogue.
/// </summary>
public static class IndexPassBatchSplitter
{
    public static Guid[][] Split(IReadOnlyList<Guid> podcastIds, int indexPasses)
    {
        if (indexPasses < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(indexPasses), "Index passes must be greater than 0.");
        }

        ArgumentNullException.ThrowIfNull(podcastIds);

        var batchSize = podcastIds.Count / indexPasses;
        var batches = new Guid[indexPasses][];
        for (var i = 0; i < indexPasses; i++)
        {
            var batch = podcastIds.Skip(i * batchSize);
            if (i < indexPasses - 1)
            {
                batch = batch.Take(batchSize);
            }

            batches[i] = batch.ToArray();
        }

        var batchSum = batches.Sum(batch => batch.Length);
        if (batchSum != podcastIds.Count)
        {
            throw new InvalidOperationException(
                $"Batch sum {batchSum} does not equal podcast id count {podcastIds.Count}.");
        }

        return batches;
    }
}
