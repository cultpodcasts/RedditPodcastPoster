using RedditPodcastPoster.Discovery.ML.Services;

namespace DiscoveryTrainingExport;

internal static class CsvParser
{
    public static IEnumerable<string[]> ReadRows(string path) => TrainingCsv.ReadRows(path);

    public static Dictionary<string, int> ParseHeader(string path) => TrainingCsv.ReadHeader(path);

    public static string GetField(string[] fields, IReadOnlyDictionary<string, int> header, string name) =>
        TrainingCsv.GetField(fields, header, name);
}
