using System.Net.Http.Headers;

namespace RedditPodcastPoster.Discovery.ML.Services;

public static class OnnxModelDownloader
{
    private const string DefaultRepo = "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main";

    public static async Task EnsureMiniLmModelAsync(string modelDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(modelDirectory);
        var modelPath = Path.Combine(modelDirectory, "model.onnx");
        var vocabPath = Path.Combine(modelDirectory, "vocab.txt");

        if (File.Exists(modelPath) && File.Exists(vocabPath))
        {
            return;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DiscoveryTrainingTrain", "1.0"));

        if (!File.Exists(modelPath))
        {
            await DownloadFileAsync(client, $"{DefaultRepo}/onnx/model.onnx", modelPath, cancellationToken);
        }

        if (!File.Exists(vocabPath))
        {
            await DownloadFileAsync(client, $"{DefaultRepo}/vocab.txt", vocabPath, cancellationToken);
        }
    }

    private static async Task DownloadFileAsync(HttpClient client, string url, string destination, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Downloading {url} ...");
        await using var stream = await client.GetStreamAsync(url, cancellationToken);
        await using var file = File.Create(destination);
        await stream.CopyToAsync(file, cancellationToken);
        Console.WriteLine($"Saved {destination}");
    }
}
