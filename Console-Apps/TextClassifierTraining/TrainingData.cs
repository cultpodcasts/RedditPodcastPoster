using RedditPodcastPoster.Models;

namespace TextClassifierTraining;

[CosmosSelector(ModelType.TrainingData)]
public class TrainingData : CosmosSelector
{
    public static readonly string PartitionKey = ModelType.TrainingData.ToString();

    public TrainingData(Guid id, string title, string description, string[] subjects) : base(id, ModelType.TrainingData)
    {
        Title = title;
        Description = description;
        Subjects = subjects;
    }

    public string Title { get; init; }
    public string Description { get; init; }
    public string[] Subjects { get; init; }
}