using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.News;

[CosmosSelector(ModelType.NewsOrganisation)]
public sealed class NewsOrganisation : Publisher
{
    public NewsOrganisation()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.NewsOrganisation;
    }

    public NewsOrganisation(string name) : this()
    {
        Name = name;
        FileKey = FileKeyFactory.GetNewsOrganisationFileKey(name);
    }
}
