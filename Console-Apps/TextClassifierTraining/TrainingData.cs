using RedditPodcastPoster.Models;

namespace TextClassifierTraining;

[CosmosSelector(ModelType.TrainingData)]
public class TrainingData : CosmosSelector
{
    public static readonly string PartitionKey = ModelType.TrainingData.ToString();

    public TrainingData(Guid id, string title, string description, string flair) : base(id, ModelType.TrainingData)
    {
        Title = title;
        Description = description;
        Flair = flair;
    }

    public string Title { get; init; }
    public string Description { get; init; }
    public string Flair { get; init; }
}