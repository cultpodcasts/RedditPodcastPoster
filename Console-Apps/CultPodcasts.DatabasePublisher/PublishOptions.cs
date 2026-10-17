namespace CultPodcasts.DatabasePublisher;

/// <summary>
/// Command-line options. The output folder is the current directory and must hold the files of the previous
/// publish: they are the baseline the run is checked against. <c>--initial</c> is the explicit statement that
/// there are none (a first publish); without it an empty folder is refused, and with it a populated folder is
/// refused. <c>--accept-removal &lt;file key or id&gt;</c> (repeatable) accepts one file that would otherwise
/// vanish unexplained. Any other argument, notably the legacy <c>--use-v2</c> that update.ps1 still passes, is
/// ignored as before.
/// </summary>
public sealed record PublishOptions(bool Initial = false, IReadOnlyList<string>? AcceptedRemovals = null)
{
    private const string InitialArgument = "--initial";
    private const string AcceptRemovalArgument = "--accept-removal";

    public IReadOnlyList<string> Accepted => AcceptedRemovals ?? [];

    public static PublishOptions Parse(IReadOnlyList<string> args)
    {
        var initial = false;
        var accepted = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Equals(InitialArgument, StringComparison.OrdinalIgnoreCase))
            {
                initial = true;
            }
            else if (arg.Equals(AcceptRemovalArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{AcceptRemovalArgument} needs the file key or id of the removal to accept.");
                }

                accepted.Add(args[++i]);
            }
            else if (arg.StartsWith(AcceptRemovalArgument + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[(AcceptRemovalArgument.Length + 1)..];
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"{AcceptRemovalArgument}= needs the file key or id of the removal to accept.");
                }

                accepted.Add(value);
            }
        }

        return new PublishOptions(initial, accepted);
    }
}
