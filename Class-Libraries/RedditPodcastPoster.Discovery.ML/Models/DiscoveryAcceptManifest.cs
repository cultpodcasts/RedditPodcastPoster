using System.Globalization;
using RedditPodcastPoster.Discovery.ML.Services;

namespace RedditPodcastPoster.Discovery.ML.Models;

public sealed class DiscoveryAcceptManifest
{
    /// <summary>
    /// 1: raw show accept rate, no calibration, threshold from settings.
    /// 2: smoothed show rate + seen count, calibrated probability, threshold chosen at training for <see cref="TargetRecall"/>.
    /// </summary>
    public const int CurrentFeatureVersion = 2;

    /// <summary>Unique per trained bundle, e.g. <c>2.20261023T2130Z</c> (feature version, then UTC training time). Used as the blob archive folder.</summary>
    public required string ModelVersion { get; set; }

    public int FeatureVersion { get; set; } = 1;

    public required DateTime TrainedAt { get; set; }

    /// <summary>Discovery time of the earliest and latest labelled result trained on.</summary>
    public DateTime? LabelsFrom { get; set; }

    public DateTime? LabelsTo { get; set; }

    public static string CreateModelVersion(DateTime trainedAtUtc) =>
        $"{CurrentFeatureVersion}.{trainedAtUtc.ToString("yyyyMMdd'T'HHmm'Z'", CultureInfo.InvariantCulture)}";

    public required int TrainingRows { get; set; }

    public int CalibrationRows { get; set; }

    public int TestRows { get; set; }

    public required int EmbeddingDimensions { get; set; }

    public required float AutoHideThreshold { get; set; }

    public float? TargetRecall { get; set; }

    public required float TestPrecisionAtThreshold { get; set; }

    public required float TestRecallAtThreshold { get; set; }

    public float ShowRatePrior { get; set; }

    public float ShowRatePriorWeight { get; set; }

    public int ShowSeenCountCap { get; set; } = ShowAcceptSmoothing.DefaultSeenCountCap;

    /// <summary>
    /// Feature version 2+ needs <c>show-accept-rates.csv</c>. Without that table every show looks unseen
    /// and only the prior applies.
    /// </summary>
    public bool RequiresShowAcceptRates => FeatureVersion >= 2;

    public ShowAcceptSmoothing ShowSmoothing() =>
        FeatureVersion >= 2
            ? new ShowAcceptSmoothing(ShowRatePrior, ShowRatePriorWeight, ShowSeenCountCap)
            : ShowAcceptSmoothing.Raw;
}
