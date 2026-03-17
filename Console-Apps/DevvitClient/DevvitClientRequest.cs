using CommandLine;

namespace DevvitClient;

public class DevvitClientRequest
{
    [Option('e', "episode-id", Required = true, HelpText = "Episode id to load and post")]
    public Guid EpisodeId { get; set; }
}
