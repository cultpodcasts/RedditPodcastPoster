using System.Globalization;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using Spectre.Console;

namespace D1SearchReconcile;

/// <summary>
/// The same Spectre.Console progress columns as <c>CosmosDbDownloader</c> and
/// <c>CultPodcasts.DatabasePublisher</c>, including <see cref="RemainingTimeColumn"/> for the ETA
/// and <see cref="ElapsedTimeColumn"/> so a long wait still ticks while a total is unknown.
/// Kept in the console app so the class library stays free of console UI.
/// </summary>
internal static class D1SearchReconcileProgressBar
{
    public static Progress Create() =>
        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn());

    public static IProgress<D1SearchReconcileProgress> Bind(ProgressContext ctx) => new Renderer(ctx);

    internal static string Describe(D1SearchReconcileProgress progress)
    {
        var name = progress.Phase.ToString();
        var completed = progress.Completed.ToString("N0", CultureInfo.InvariantCulture);
        var label = progress.Total is { } total
            ? $"{name} ({completed}/{total.ToString("N0", CultureInfo.InvariantCulture)})"
            : $"{name} ({completed})";
        return string.IsNullOrEmpty(progress.Detail) ? label : $"{label} {progress.Detail}";
    }

    private sealed class Renderer(ProgressContext ctx) : IProgress<D1SearchReconcileProgress>
    {
        private readonly Dictionary<D1SearchReconcilePhase, ProgressTask> _tasks = [];

        public void Report(D1SearchReconcileProgress value)
        {
            if (!_tasks.TryGetValue(value.Phase, out var task))
            {
                var max = value.Total is > 0 ? value.Total.Value : 1;
                task = ctx.AddTask(Describe(value), maxValue: max);
                _tasks[value.Phase] = task;
            }

            task.Description = Describe(value);
            if (value.Total is { } total)
            {
                task.IsIndeterminate = false;
                task.MaxValue = Math.Max(total, 1);
                task.Value = total == 0 ? task.MaxValue : Math.Clamp(value.Completed, 0, (int)task.MaxValue);
                if (total == 0 || value.Completed >= total)
                {
                    task.StopTask();
                }
            }
            else
            {
                task.IsIndeterminate = true;
            }
        }
    }
}
