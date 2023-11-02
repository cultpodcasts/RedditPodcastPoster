using CommandLine;

namespace RedditPodcastPoster.JsonSplitCosmosDbUploader;

public class JsonSplitCosmosDbUploadRequest
{
    [Value(0, HelpText = "Filename of Json file with podcast to split", Required = true)]
    public required string FileName { get; set; }

}