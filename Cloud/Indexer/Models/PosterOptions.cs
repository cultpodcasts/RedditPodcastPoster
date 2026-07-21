namespace Indexer.Models;

public class PosterOptions
{
    public int ReleasedDaysAgo { get; set; }

    public int? MaxPosts { get; set; }

    public override string ToString()
    {
        return
            $"{nameof(PosterOptions)} {{{nameof(ReleasedDaysAgo)}: '{ReleasedDaysAgo}', {nameof(MaxPosts)}: '{MaxPosts}'}}.";
    }
}