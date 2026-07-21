using Microsoft.ML;
using RedditPodcastPoster.Discovery.ML.Models;

namespace RedditPodcastPoster.Discovery.ML.Services;

public sealed class DiscoveryAcceptModelPredictor : IDisposable
{
    private readonly MLContext _mlContext = new();
    private readonly PredictionEngine<DiscoveryTrainingExample, DiscoveryPrediction> _engine;

    public DiscoveryAcceptModelPredictor(string modelPath)
    {
        using var stream = File.OpenRead(modelPath);
        var model = _mlContext.Model.Load(stream, out _);
        _engine = _mlContext.Model.CreatePredictionEngine<DiscoveryTrainingExample, DiscoveryPrediction>(model);
    }

    public float PredictAcceptProbability(DiscoveryTrainingExample example)
    {
        return _engine.Predict(example).Probability;
    }

    public void Dispose()
    {
        _engine.Dispose();
    }
}
