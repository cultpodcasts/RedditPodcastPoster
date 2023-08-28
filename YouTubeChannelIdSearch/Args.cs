using CommandLine;

namespace YouTubeChannelIdSearch;

public class Args
{
    [Value(0, MetaName = "channel-name", HelpText = "Name of the YouTube channel", Required = true)]
    public string ChannelName { get; set; } = "";

    [Value(1, MetaName = "most-recent-upload-name",
        HelpText = "Name of the most recently uploaded video to YouTube by this channel", Required = true)]
    public string MostRecentUploadedVideoTitle { get; set; } = "";

}