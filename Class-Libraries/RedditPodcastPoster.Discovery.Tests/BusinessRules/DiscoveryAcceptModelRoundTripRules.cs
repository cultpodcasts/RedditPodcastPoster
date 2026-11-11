using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Discovery.ML.Models;
using RedditPodcastPoster.Discovery.ML.Services;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

public class DiscoveryAcceptModelRoundTripRules : IDisposable
{
    private const int MinimumTrainerExamples = 100;
    private readonly Fixture _fixture = new();
    private readonly string _outputDirectory = Path.Combine(
        Path.GetTempPath(),
        "discovery-accept-roundtrip",
        Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact(DisplayName =
        "A model written by DiscoveryAcceptModelTrainer loads through DiscoveryAcceptModelPredictor, because Discover scores the zip format that trainer saves.")]
    public async Task trainer_zip_loads_in_the_predictor()
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var trainer = new DiscoveryAcceptModelTrainer();

        // Act
        var trained = await trainer.TrainAsync(examples, _outputDirectory);
        using var predictor = new DiscoveryAcceptModelPredictor(trained.ModelPath);
        var probability = predictor.PredictAcceptProbability(examples[0]);

        // Assert
        probability.Should().BeInRange(0f, 1f);
    }

    [Fact(DisplayName =
        "Training writes a feature-version-2 manifest with a threshold, smoothing prior and labelled date range, because Discover reads them to score and hide results the same way training did.")]
    public async Task trainer_writes_a_version_two_manifest()
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var trainer = new DiscoveryAcceptModelTrainer();

        // Act
        var trained = await trainer.TrainAsync(examples, _outputDirectory);

        // Assert
        trained.Manifest.FeatureVersion.Should().Be(DiscoveryAcceptManifest.CurrentFeatureVersion);
        trained.Manifest.ModelVersion.Should().StartWith($"{DiscoveryAcceptManifest.CurrentFeatureVersion}.");
        trained.Manifest.AutoHideThreshold.Should().BeInRange(0f, 1f);
        trained.Manifest.ShowRatePrior.Should().BeInRange(0f, 1f);
        trained.Manifest.LabelsFrom.Should().Be(examples.Min(x => x.DiscoveryBegan));
        trained.Manifest.LabelsTo.Should().Be(examples.Max(x => x.DiscoveryBegan));
        File.Exists(trained.ManifestPath).Should().BeTrue();
        File.Exists(trained.MetricsPath).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A fixed threshold option is written to the manifest instead of a recall-chosen one, because an operator override must be what Discover applies.")]
    public async Task fixed_threshold_overrides_target_recall()
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var threshold = 0.25f;
        var trainer = new DiscoveryAcceptModelTrainer();

        // Act
        var trained = await trainer.TrainAsync(
            examples,
            _outputDirectory,
            new DiscoveryAcceptTrainOptions { FixedThreshold = threshold });

        // Assert
        trained.Manifest.AutoHideThreshold.Should().Be(threshold);
        trained.Manifest.TargetRecall.Should().BeNull();
    }

    [Fact(DisplayName =
        "Training leaves the caller's examples unchanged, because show rates and example weights are training state rather than part of the labelled input.")]
    public async Task training_does_not_change_the_callers_examples()
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var before = examples
            .Select(example => (example.ShowAcceptRate, example.ShowSeenCount, example.ExampleWeight, example.Embedding[0]))
            .ToList();
        var trainer = new DiscoveryAcceptModelTrainer(); // pragma: allowlist secret

        // Act
        await trainer.TrainAsync(examples, _outputDirectory);

        // Assert
        examples
            .Select(example => (example.ShowAcceptRate, example.ShowSeenCount, example.ExampleWeight, example.Embedding[0]))
            .Should()
            .Equal(before);
    }

    [Fact(DisplayName =
        "Training refuses a split that leaves a slice with no discovery run, because the hide threshold and the test metrics must come from later runs than the ones the model trains on.")] // pragma: allowlist secret
    public async Task training_throws_when_every_row_shares_one_discovery_run() // pragma: allowlist secret
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var began = examples[0].DiscoveryBegan; // pragma: allowlist secret
        foreach (var example in examples)
        {
            example.DiscoveryBegan = began; // pragma: allowlist secret
        }

        var trainer = new DiscoveryAcceptModelTrainer(); // pragma: allowlist secret

        // Act
        var train = () => trainer.TrainAsync(examples, _outputDirectory);

        // Assert
        await train.Should().ThrowAsync<InvalidOperationException>().WithMessage("*slice empty*");
    }

    [Fact(DisplayName =
        "Training writes show accept counts over every labelled row, because Discover scores new results with each show's full history.")]
    public async Task trainer_writes_show_accept_counts_for_every_show()
    {
        // Arrange
        var examples = CreateSeparableExamples(MinimumTrainerExamples);
        var trainer = new DiscoveryAcceptModelTrainer();

        // Act
        var trained = await trainer.TrainAsync(examples, _outputDirectory);
        var counts = ShowAcceptRateLookup.Load(trained.ShowAcceptRatesPath);

        // Assert
        foreach (var show in examples.GroupBy(x => x.ShowName))
        {
            counts[show.Key!].Should().Be(new ShowAcceptCounts(show.Count(x => x.Label), show.Count()));
        }
    }

    private List<DiscoveryTrainingExample> CreateSeparableExamples(int count)
    {
        var acceptedShow = _fixture.Create<string>();
        var rejectedShow = _fixture.Create<string>();
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
                ShowName = accepted ? acceptedShow : rejectedShow
            });
        }

        return examples;
    }
}
