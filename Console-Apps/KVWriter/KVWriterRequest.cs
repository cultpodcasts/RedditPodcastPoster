using CommandLine;

namespace KVWriter;

public class KVWriterRequest
{
    [Option('i', "items", Default = 2, HelpText = "Number of items to take")]
    public int ItemsToTake { get; set; }
}