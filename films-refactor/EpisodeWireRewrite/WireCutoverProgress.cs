using System.Diagnostics;

namespace EpisodeWireRewrite;

public sealed class WireCutoverProgressSnapshot
{
    public required int Scanned { get; init; }
    public required int NeedChange { get; init; }
    public required int Written { get; init; }
    public required int Failed { get; init; }
    public required int SkippedUnchanged { get; init; }
    public Guid? LastEpisodeId { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required bool Apply { get; init; }
    public required string Mode { get; init; }

    public double PerSecond =>
        Elapsed.TotalSeconds <= 0 ? 0 : Scanned / Elapsed.TotalSeconds;
}

public interface IWireCutoverProgressReporter
{
    void Report(WireCutoverProgressSnapshot snapshot);
    void Completed(WireCutoverProgressSnapshot snapshot, string summary);
}

/// <summary>Console progress — key operator surface during long GATE 1 runs.</summary>
public sealed class ConsoleWireCutoverProgressReporter : IWireCutoverProgressReporter
{
    private readonly object _gate = new();
    private readonly TextWriter _output;

    public ConsoleWireCutoverProgressReporter(TextWriter? output = null)
    {
        _output = output ?? Console.Out;
    }

    public void Report(WireCutoverProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var line =
            $"[{DateTime.UtcNow:HH:mm:ss}Z] {snapshot.Mode} apply={snapshot.Apply} " +
            $"scanned={snapshot.Scanned} needChange={snapshot.NeedChange} written={snapshot.Written} " +
            $"failed={snapshot.Failed} unchanged={snapshot.SkippedUnchanged} " +
            $"rate={snapshot.PerSecond:0.0}/s elapsed={snapshot.Elapsed:hh\\:mm\\:ss} " +
            $"last={snapshot.LastEpisodeId}";
        lock (_gate)
        {
            _output.WriteLine(line);
        }
    }

    public void Completed(WireCutoverProgressSnapshot snapshot, string summary)
    {
        Report(snapshot);
        lock (_gate)
        {
            _output.WriteLine(summary);
        }
    }
}

public sealed class WireCutoverProgressTracker
{
    private readonly IWireCutoverProgressReporter _reporter;
    private readonly int _every;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private int _scanned;
    private int _needChange;
    private int _written;
    private int _failed;
    private int _unchanged;
    private Guid? _lastEpisodeId;

    public WireCutoverProgressTracker(IWireCutoverProgressReporter reporter, int progressEvery)
    {
        _reporter = reporter;
        _every = Math.Max(1, progressEvery);
    }

    public void ScannedOne(Guid? episodeId = null)
    {
        var n = Interlocked.Increment(ref _scanned);
        if (episodeId.HasValue)
        {
            _lastEpisodeId = episodeId;
        }

        MaybeReport(n);
    }

    public void NeedChange() => Interlocked.Increment(ref _needChange);

    public void Written(Guid episodeId)
    {
        Interlocked.Increment(ref _written);
        _lastEpisodeId = episodeId;
    }

    public void Failed() => Interlocked.Increment(ref _failed);

    public void Unchanged() => Interlocked.Increment(ref _unchanged);

    public WireCutoverProgressSnapshot Snapshot(bool apply, string mode) => new()
    {
        Scanned = Volatile.Read(ref _scanned),
        NeedChange = Volatile.Read(ref _needChange),
        Written = Volatile.Read(ref _written),
        Failed = Volatile.Read(ref _failed),
        SkippedUnchanged = Volatile.Read(ref _unchanged),
        LastEpisodeId = _lastEpisodeId,
        Elapsed = _clock.Elapsed,
        Apply = apply,
        Mode = mode
    };

    public void ForceReport(bool apply, string mode) =>
        _reporter.Report(Snapshot(apply, mode));

    public void Complete(bool apply, string mode, string summary) =>
        _reporter.Completed(Snapshot(apply, mode), summary);

    private void MaybeReport(int scanned)
    {
        if (scanned % _every == 0)
        {
            // Mode filled by caller via ForceReport in host; tracker uses placeholder.
        }
    }
}
