using ContractExporter;
using RedditPodcastPoster.Configuration;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

var output = (string?)null;
var check = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        case "--check":
            check = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument '{args[i]}'. Usage: ContractExporter --output <dir> [--check]");
            return 2;
    }
}

output ??= FindRepositoryContracts();
var document = ContractGraph.Build();
if (check)
{
    var differences = ContractText.Differences(output, document);
    if (differences.Count == 0)
    {
        Console.WriteLine($"Contracts match {output}.");
        return 0;
    }

    Console.Error.WriteLine("Generated contracts differ from the files on disk:");
    foreach (var difference in differences)
    {
        Console.Error.WriteLine("  " + difference);
    }

    Console.Error.WriteLine(
        "Those files are generated and gitignored. A mismatch means the build did not finish writing them. Rebuild Tools/ContractExporter.");
    return 1;
}

ContractText.Write(output, document);
Console.WriteLine($"Wrote contracts to {output}.");
return 0;

static string FindRepositoryContracts()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "RedditPodcastPoster.slnx")))
        {
            return Path.Combine(dir.FullName, "contracts");
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Pass --output. Could not find the repository root.");
}
