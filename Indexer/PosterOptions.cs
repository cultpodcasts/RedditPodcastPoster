namespace Indexer;

public class PosterOptions
{
    public int ReleasedDaysAgo { get; set; }

    public override string ToString()
    {
        return
            $"IndexerOptions {{ {nameof(ReleasedDaysAgo)}: '{ReleasedDaysAgo}'.";
    }
}