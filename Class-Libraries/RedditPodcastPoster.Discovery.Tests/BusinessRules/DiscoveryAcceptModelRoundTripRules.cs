using FluentAssertions;
using RedditPodcastPoster.Discovery.ML.Models;
using RedditPodcastPoster.Discovery.ML.Services;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

public class DiscoveryAcceptModelRoundTripRules
{
    private const int MinimumTrainerExamples = 100;

    [Fact(DisplayName =
        "A model written by DiscoveryAcceptModelTrainer loads through DiscoveryAcceptModelPredictor, because Discover scores the zip format that trainer saves.")]
    public async Task trainer_zip_loads_in_the_predictor()
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "discovery-accept-roundtrip",
            Path.GetRandomFileName());
        var trainer = new DiscoveryAcceptModelTrainer();

        try
        {
            // Act
            var trained = await trainer.TrainAsync(examples, outputDirectory, autoHideThreshold: 0.5f);
            using var predictor = new DiscoveryAcceptModelPredictor(trained.ModelPath);
            var probability = predictor.PredictAcceptProbability(examples[0]);

            // Assert
            probability.Should().BeInRange(0f, 1f);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static List<DiscoveryTrainingExample> CreateSeparableExamples(int count)
    {
        var examples = new List<DiscoveryTrainingExample>(count);
        var began = DateTime.UtcNow.AddDays(-count);
        for (var index = 0; index < count; index++)
        {
            var accepted = index % 2 == 0;
            var embedding = new float[DiscoveryFeatureBuilder.EmbeddingDimensions];
            embedding[0] = accepted ? 1f : 0f;
            examples.Add(new DiscoveryTrainingExample
            {
                DiscoveryBegan = began.AddHours(index),
                Label = accepted,
                Embedding = embedding,
                HasMatchingPodcast = accepted ? 1f : 0f,
                ShowAcceptRate = accepted ? 1f : 0f
            });
        }

        return examples;
    }
}
