namespace CultPodcasts.DatabasePublisher;

/// <summary>
/// Command-line options. The output folder is the current directory and must hold the files of the previous
/// publish: they are the baseline the run is checked against. <c>--initial</c> is the explicit statement that
/// there are none (a first publish); without it an empty folder is refused, and with it a populated folder is
/// refused. <c>--accept-removal &lt;file key or id&gt;</c> (repeatable) accepts one file that would otherwise
/// vanish unexplained. <c>--discard-staging</c> deletes the <c>*.json.tmp</c> files an interrupted run left in
/// the folder (without it their presence refuses the run). <c>--migrations-out &lt;path&gt;</c> writes the
/// migration manifest there after a successful publish; the path must be outside the output folder. Any other
/// argument, notably the legacy <c>--use-v2</c> that update.ps1 still passes, is ignored as before.
/// </summary>
public sealed record PublishOptions(
    bool Initial = false,
    IReadOnlyList<string>? AcceptedRemovals = null,
    bool DiscardStaging = false,
    string? MigrationsOut = null)
{
    private const string InitialArgument = "--initial";
    private const string AcceptRemovalArgument = "--accept-removal";
    private const string DiscardStagingArgument = "--discard-staging";
    private const string MigrationsOutArgument = "--migrations-out";

    public IReadOnlyList<string> Accepted => AcceptedRemovals ?? [];

    public static PublishOptions Parse(IReadOnlyList<string> args)
    {
        var initial = false;
        var discardStaging = false;
        string? migrationsOut = null;
        var accepted = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Equals(InitialArgument, StringComparison.OrdinalIgnoreCase))
            {
                initial = true;
            }
            else if (arg.Equals(DiscardStagingArgument, StringComparison.OrdinalIgnoreCase))
            {
                discardStaging = true;
            }
            else if (arg.Equals(AcceptRemovalArgument, StringComparison.OrdinalIgnoreCase))
            {
                accepted.Add(ValueOf(args, ref i, AcceptRemovalArgument, "the file key or id of the removal to accept"));
            }
            else if (arg.StartsWith(AcceptRemovalArgument + "=", StringComparison.OrdinalIgnoreCase))
            {
                accepted.Add(InlineValue(arg, AcceptRemovalArgument, "the file key or id of the removal to accept"));
            }
            else if (arg.Equals(MigrationsOutArgument, StringComparison.OrdinalIgnoreCase))
            {
                migrationsOut = ValueOf(args, ref i, MigrationsOutArgument, "the path of the file to write");
            }
            else if (arg.StartsWith(MigrationsOutArgument + "=", StringComparison.OrdinalIgnoreCase))
            {
                migrationsOut = InlineValue(arg, MigrationsOutArgument, "the path of the file to write");
            }
        }

        return new PublishOptions(initial, accepted, discardStaging, migrationsOut);
    }

    private static string ValueOf(IReadOnlyList<string> args, ref int i, string name, string needs)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{name} needs {needs}.");
        }

        return args[++i];
    }

    private static string InlineValue(string arg, string name, string needs)
    {
        var value = arg[(name.Length + 1)..];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name}= needs {needs}.");
        }

        return value;
    }
}
