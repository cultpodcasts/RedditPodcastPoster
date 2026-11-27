namespace Indexer.Services;

/// <summary>
/// Pure schedule for the two indexer passes that run each UTC hour: Spotify discovery every
/// run, expensive Spotify twice per batch per day, expensive YouTube once per day rotating
/// over the four batches. Pass pairing (even → 1–2, odd → 3–4) and YouTube <c>hour % 3</c>
/// resolving are unchanged.
/// </summary>
public static class HourlyIndexingSchedule
{
    public sealed record PassPlan(
        int Pass,
        bool IndexSpotify,
        bool ResolveYouTube,
        bool ExpensiveSpotify,
        bool ExpensiveYouTube);

    /// <summary>Returns the two <see cref="PassPlan"/>s that run at <paramref name="utcNow"/>.</summary>
    public static IReadOnlyList<PassPlan> For(DateTime utcNow)
    {
        var hour = utcNow.Hour;
        var day = utcNow.DayOfYear;
        var first = hour % 2 == 0 ? 1 : 3;
        var resolveYouTube = hour % 3 == 0;

        // Spotify discovery on every run (the 2-hourly batch cadence already halves the calls).
        const bool indexSpotify = true;

        // Expensive Spotify: lower pair at hours 0,6,12,18; upper pair at hours 3,9,15,21.
        // Alternate which pass of the pair gets it, so each batch gets it twice a day.
        int? expensiveSpotifyPass = hour % 6 == 0 ? first + (hour / 6) % 2
            : hour % 6 == 3 ? first + (hour / 6) % 2
            : null;

        // Expensive YouTube: once a day (same quota as today), rotating over the 4 batches.
        // day % 4: 0 → hour 0 pass 1; 1 → hour 0 pass 2; 2 → hour 3 pass 3; 3 → hour 3 pass 4.
        int? expensiveYouTubePass = (day % 4, hour) switch
        {
            (0, 0) => 1,
            (1, 0) => 2,
            (2, 3) => 3,
            (3, 3) => 4,
            _ => null
        };

        return new[] { first, first + 1 }
            .Select(pass => new PassPlan(
                pass,
                indexSpotify,
                resolveYouTube,
                pass == expensiveSpotifyPass,
                pass == expensiveYouTubePass))
            .ToList();
    }

    /// <summary>Pass plan for <paramref name="pass"/> at <paramref name="utcNow"/>, or null when that pass does not run.</summary>
    public static PassPlan? ForPass(DateTime utcNow, int pass) =>
        For(utcNow).FirstOrDefault(plan => plan.Pass == pass);
}
