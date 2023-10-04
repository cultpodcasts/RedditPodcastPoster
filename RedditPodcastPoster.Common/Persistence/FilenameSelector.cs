using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public class FilenameSelector : IFilenameSelector
{
    public string GetKey(Podcast podcast)
    {
        return podcast.FileKey;
    }

    public string GetKey(EliminationTerms.EliminationTerms eliminationTerms)
    {
        return nameof(ModelType.EliminationTerms);
    }

    public string GetKey(KnownTerms.KnownTerms knownTerms)
    {
        return nameof(ModelType.KnownTerms);
    }
}