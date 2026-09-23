using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.TvShows;

[CosmosSelector(ModelType.TvShow)]
public sealed class TvShow : Publisher
{
    public TvShow()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.TvShow;
    }

    public TvShow(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetTvShowFileKey(name);
    }
}
