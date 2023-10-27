using CommandLine;

namespace Discover;

public class DiscoveryRequest
{
    //[Option('f', "submit-urls-in-file", Required = false, HelpText = "Use urls in provided file",
    //    Default = false)]
    //public bool SubmitUrlsInFile { get; set; }

    [Value(0, MetaName = "number-of-days", HelpText = "The number of days to search within", Required = false,
        Default = 1)]
    public int NumberOfDays { get; set; }
}