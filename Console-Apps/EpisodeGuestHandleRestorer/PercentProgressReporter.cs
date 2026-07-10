namespace EpisodeGuestHandleRestorer;

internal sealed class PercentProgressReporter(string label)
{
    private static int _lastProgressLength;
    private int _lastReportedPercent = -1;
    private int _lastReportedAtCount;

    public void Report(int current, int total, bool force = false)
    {
        if (total <= 0)
        {
            return;
        }

        var percent = current * 100 / total;
        var finalize = force || current >= total;
        if (!finalize)
        {
            var percentCrossed = percent > _lastReportedPercent;
            var countThreshold = current - _lastReportedAtCount >= 5000;
            if (!percentCrossed && !countThreshold)
            {
                return;
            }
        }

        _lastReportedPercent = percent;
        _lastReportedAtCount = current;
        WriteLine($"{label}: {current}/{total} ({percent}%)", finalize);
    }

    public static void ClearLine()
    {
        if (_lastProgressLength <= 0)
        {
            return;
        }

        Console.Write($"\r{new string(' ', _lastProgressLength)}\r");
        _lastProgressLength = 0;
        Console.Out.Flush();
    }

    private static void WriteLine(string message, bool finalize)
    {
        var padding = Math.Max(0, _lastProgressLength - message.Length);
        if (finalize)
        {
            Console.WriteLine($"\r{message}");
            _lastProgressLength = 0;
        }
        else
        {
            Console.Write($"\r{message}{new string(' ', padding)}");
            _lastProgressLength = message.Length;
        }

        Console.Out.Flush();
    }
}
