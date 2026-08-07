using CommandLine;

namespace EpisodeUpdateTimingProbe;

public class EpisodeUpdateTimingProbeRequest
{
    [Option('e', "episode-id", Required = true, HelpText = "Episode GUID to resolve and (optionally) re-index.")]
    public Guid EpisodeId { get; set; }

    [Option('p', "podcast-id", Required = false, HelpText = "Podcast GUID (matches API resolve path when provided).")]
    public Guid? PodcastId { get; set; }

    [Option("skip-index", Required = false, Default = false,
        HelpText = "Skip Azure Search IndexEpisode.")]
    public bool SkipIndex { get; set; }

    [Option("skip-homepage", Required = false, Default = false,
        HelpText = "Skip PublishHomepage (Cosmos assemble + R2 upload).")]
    public bool SkipHomepage { get; set; }

    [Option("parallel", Required = false, Default = false,
        HelpText = "Also time IndexEpisode + PublishHomepage under Task.WhenAll (second pass; repeats side effects).")]
    public bool Parallel { get; set; }
}
