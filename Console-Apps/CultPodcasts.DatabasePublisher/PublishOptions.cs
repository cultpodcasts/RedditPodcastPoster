namespace CultPodcasts.DatabasePublisher;

/// <summary>
/// Command-line options. <c>--baseline &lt;folder&gt;</c> is the folder holding the files of the previous
/// publish; with it the run reports (and, unless accepted, fails on) files that stop being published.
/// Any other argument, notably the legacy <c>--use-v2</c> that update.ps1 still passes, is ignored as before.
/// </summary>
public sealed record PublishOptions(string? BaselinePath = null, bool AcceptUnexplainedRemovals = false)
{
    private const string BaselineArgument = "--baseline";
    private const string AcceptUnexplainedRemovalsArgument = "--accept-unexplained-removals";

    public static PublishOptions Parse(IReadOnlyList<string> args)
    {
        string? baselinePath = null;
        var acceptUnexplainedRemovals = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Equals(BaselineArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{BaselineArgument} needs the folder of the previous publish.");
                }

                baselinePath = args[++i];
            }
            else if (arg.StartsWith(BaselineArgument + "=", StringComparison.OrdinalIgnoreCase))
            {
                baselinePath = arg[(BaselineArgument.Length + 1)..];
                if (string.IsNullOrWhiteSpace(baselinePath))
                {
                    throw new ArgumentException($"{BaselineArgument}= needs the folder of the previous publish.");
                }
            }
            else if (arg.Equals(AcceptUnexplainedRemovalsArgument, StringComparison.OrdinalIgnoreCase))
            {
                acceptUnexplainedRemovals = true;
            }
        }

        return new PublishOptions(baselinePath, acceptUnexplainedRemovals);
    }
}
