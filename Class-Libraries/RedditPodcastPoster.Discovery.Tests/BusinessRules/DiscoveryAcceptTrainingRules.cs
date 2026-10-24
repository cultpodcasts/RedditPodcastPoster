using System.Globalization;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Discovery.ML.Configuration;
using RedditPodcastPoster.Discovery.ML.Models;
using RedditPodcastPoster.Discovery.ML.Services;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

public class DiscoveryAcceptTrainingRules
{
    private static readonly string[] ExportHeader =
        ["state", "discoveryBegan", "showName", "episodeName", "episodeDescription", "autoHidden"];

    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "A training CSV field containing commas, quotes and line breaks reads back unchanged, because episode descriptions are multi-line text.")]
    public void multi_line_quoted_field_round_trips()
    {
        // Arrange
        var description = $"{_fixture.Create<string>()}, \"{_fixture.Create<string>()}\"\n{_fixture.Create<string>()}";
        var trailing = _fixture.Create<string>();
        var csv = $"{TrainingCsv.Escape(description)},{TrainingCsv.Escape(trailing)}\n";

        // Act
        var records = TrainingCsv.ReadRecords(new StringReader(csv)).ToList();

        // Assert
        records.Should().ContainSingle();
        records[0].Should().Equal(description, trailing);
    }

    [Fact(DisplayName =
        "Rejected results the scorer auto-hid are not curator labels, because no curator saw them; shown rejects and all accepts are.")]
    public void auto_hidden_rejects_are_not_curator_labels()
    {
        // Arrange
        var csv = ExportCsv(
            ("Accepted", true),
            ("Rejected", false),
            ("Rejected", true));

        // Act
        var labels = TrainingCsvReader.ReadLabeledRows(new StringReader(csv))
            .Where(TrainingCsvReader.IsCuratorLabel)
            .ToList();

        // Assert
        labels.Select(x => (x.Accepted, x.AutoHidden)).Should().Equal((true, true), (false, false));
    }

    [Fact(DisplayName =
        "The exported episode description is read as training text, because the model scores description as well as title.")]
    public void episode_description_is_read()
    {
        // Arrange
        var description = $"{_fixture.Create<string>()}\n{_fixture.Create<string>()}";
        var csv = string.Join(',', ExportHeader) + "\n" + string.Join(',',
            "Accepted",
            TrainingCsv.Escape(DateTime.UtcNow),
            TrainingCsv.Escape(_fixture.Create<string>()),
            TrainingCsv.Escape(_fixture.Create<string>()),
            TrainingCsv.Escape(description),
            "false") + "\n";

        // Act
        var row = TrainingCsvReader.ReadLabeledRows(new StringReader(csv)).Single();

        // Assert
        row.Description.Should().Be(description);
    }

    [Fact(DisplayName =
        "A row's show history counts only earlier discovery runs, never its own label, its own run or later runs, because training on the future leaks the answer.")]
    public void point_in_time_history_excludes_own_run_and_later()
    {
        // Arrange
        var show = _fixture.Create<string>();
        var firstRun = DateTime.UtcNow.AddDays(-3);
        var secondRun = firstRun.AddDays(1);
        var thirdRun = secondRun.AddDays(1);
        var rows = new List<(string?, DateTime, bool)>
        {
            (show, firstRun, true),
            (show, secondRun, true),
            (show, secondRun, false),
            (show, thirdRun, false)
        };
        var smoothing = new ShowAcceptSmoothing(0f, 0f, ShowAcceptSmoothing.DefaultSeenCountCap);

        // Act
        var features = ShowAcceptHistory.PointInTime(rows, smoothing);

        // Assert
        features.Select(x => x.AcceptRate).Should().Equal(0f, 1f, 1f, 2f / 3f);
        features[0].SeenCount.Should().Be(0f);
        features[1].SeenCount.Should().Be(features[2].SeenCount);
    }

    [Fact(DisplayName =
        "A show with no history scores the overall accept rate, not zero, because an unseen show is not evidence of rejection.")]
    public void unseen_show_gets_the_prior()
    {
        // Arrange
        var prior = 0.1f + _fixture.Create<int>() % 50 / 100f;
        var smoothing = new ShowAcceptSmoothing(prior, ShowAcceptSmoothing.DefaultPriorWeight, ShowAcceptSmoothing.DefaultSeenCountCap);

        // Act
        var features = smoothing.Features(null);

        // Assert
        features.AcceptRate.Should().BeApproximately(prior, 1e-6f);
        features.SeenCount.Should().Be(0f);
    }

    [Fact(DisplayName =
        "One accept from one result is shrunk toward the prior rather than scored as 100%, because a single result is weak evidence.")]
    public void single_accept_is_shrunk_toward_prior()
    {
        // Arrange
        var prior = 0.1f;
        var smoothing = new ShowAcceptSmoothing(prior, ShowAcceptSmoothing.DefaultPriorWeight, ShowAcceptSmoothing.DefaultSeenCountCap);

        // Act
        var features = smoothing.Features(new ShowAcceptCounts(1, 1));

        // Assert
        features.AcceptRate.Should().BeGreaterThan(prior).And.BeLessThan(0.5f);
        features.SeenCount.Should().BeGreaterThan(0f);
    }

    [Fact(DisplayName =
        "Feature-version-1 manifests score shows with the raw rate and zero for unknown shows, because older models were trained that way.")]
    public void version_one_manifest_uses_raw_rate()
    {
        // Arrange
        var manifest = CreateManifest(featureVersion: 1);
        var accepted = _fixture.Create<int>() % 10 + 1;
        var total = accepted * 2;

        // Act
        var smoothing = manifest.ShowSmoothing();

        // Assert
        smoothing.Features(null).AcceptRate.Should().Be(0f);
        smoothing.Features(new ShowAcceptCounts(accepted, total)).AcceptRate.Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact(DisplayName =
        "The hide threshold for a target recall keeps at least that share of accepted results visible, and is the highest such threshold.")]
    public void threshold_meets_target_recall()
    {
        // Arrange
        var scored = Enumerable.Range(1, 20)
            .Select(i => new ScoredLabel(i / 20f, i % 2 == 0))
            .ToList();
        var targetRecall = 0.9f;

        // Act
        var threshold = DiscoveryThresholds.ForTargetRecall(scored, targetRecall);
        var outcome = DiscoveryThresholds.Evaluate(scored, threshold);
        var higher = DiscoveryThresholds.Evaluate(scored, threshold + 1e-4f);

        // Assert
        outcome.Recall.Should().BeGreaterThanOrEqualTo(targetRecall);
        higher.Recall.Should().BeLessThan(targetRecall);
    }

    [Theory(DisplayName =
        "Discover hides below the manifest threshold only for feature-version-2 models with UseManifestThreshold on; otherwise it uses the settings threshold.")]
    [InlineData(2, true, true)]
    [InlineData(2, false, false)]
    [InlineData(1, true, false)]
    public void scorer_threshold_source(int featureVersion, bool useManifestThreshold, bool expectManifest)
    {
        // Arrange
        var manifest = CreateManifest(featureVersion);
        var settings = new DiscoveryScorerSettings
        {
            AutoHideThreshold = manifest.AutoHideThreshold / 2f,
            UseManifestThreshold = useManifestThreshold
        };

        // Act
        var threshold = DiscoveryResultScorer.AutoHideThreshold(manifest, settings);

        // Assert
        threshold.Should().Be(expectManifest ? manifest.AutoHideThreshold : settings.AutoHideThreshold);
    }

    [Fact(DisplayName =
        "The model version is the feature version then the UTC training minute, because each trained bundle needs a unique archive folder.")]
    public void model_version_is_feature_version_and_training_minute()
    {
        // Arrange
        var trainedAt = DateTime.UtcNow;

        // Act
        var version = DiscoveryAcceptManifest.CreateModelVersion(trainedAt);

        // Assert
        version.Should().MatchRegex(@"^\d+\.\d{8}T\d{4}Z$");
        version.Should().StartWith($"{DiscoveryAcceptManifest.CurrentFeatureVersion}.");
        DateTime.ParseExact(version[(version.IndexOf('.') + 1)..], "yyyyMMdd'T'HHmm'Z'", CultureInfo.InvariantCulture)
            .Should().Be(new DateTime(trainedAt.Year, trainedAt.Month, trainedAt.Day, trainedAt.Hour, trainedAt.Minute, 0));
    }

    private string ExportCsv(params (string State, bool AutoHidden)[] rows) =>
        string.Join(',', ExportHeader) + "\n" + string.Concat(rows.Select(row => string.Join(',',
            row.State,
            TrainingCsv.Escape(DateTime.UtcNow),
            TrainingCsv.Escape(_fixture.Create<string>()),
            TrainingCsv.Escape(_fixture.Create<string>()),
            TrainingCsv.Escape(_fixture.Create<string>()),
            TrainingCsv.Escape(row.AutoHidden)) + "\n"));

    private DiscoveryAcceptManifest CreateManifest(int featureVersion) => new()
    {
        ModelVersion = _fixture.Create<string>(),
        FeatureVersion = featureVersion,
        TrainedAt = DateTime.UtcNow,
        TrainingRows = _fixture.Create<int>(),
        EmbeddingDimensions = DiscoveryFeatureBuilder.EmbeddingDimensions,
        AutoHideThreshold = 0.1f + _fixture.Create<int>() % 50 / 100f,
        TestPrecisionAtThreshold = 0.5f,
        TestRecallAtThreshold = 0.97f,
        ShowRatePrior = 0.1f,
        ShowRatePriorWeight = ShowAcceptSmoothing.DefaultPriorWeight
    };
}
