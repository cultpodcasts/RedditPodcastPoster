namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>A decoded thumbnail as packed RGB bytes (3 bytes per pixel, row-major).</summary>
public sealed record ThumbnailFrame(int Width, int Height, byte[] Rgb)
{
    /// <summary>Pixels sampled on every second row and column (matches the calibration script).</summary>
    public const int SampleStep = 2;

    /// <summary>Mean absolute RGB difference (0-255) between two frames of the same size.</summary>
    public static double MeanAbsoluteDifference(ThumbnailFrame a, ThumbnailFrame b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            throw new ArgumentException("Frames must be the same size.");
        }

        long sum = 0;
        long count = 0;
        for (var y = 0; y < a.Height; y += SampleStep)
        {
            for (var x = 0; x < a.Width; x += SampleStep)
            {
                var i = (y * a.Width + x) * 3;
                for (var c = 0; c < 3; c++)
                {
                    sum += Math.Abs(a.Rgb[i + c] - b.Rgb[i + c]);
                    count++;
                }
            }
        }

        return count == 0 ? 0 : (double)sum / count;
    }

    /// <summary>Standard deviation (0-255) of all sampled channel values: near zero for blank/placeholder images.</summary>
    public double PixelStandardDeviation()
    {
        double sum = 0;
        double sumSquares = 0;
        long count = 0;
        for (var y = 0; y < Height; y += SampleStep)
        {
            for (var x = 0; x < Width; x += SampleStep)
            {
                var i = (y * Width + x) * 3;
                for (var c = 0; c < 3; c++)
                {
                    double v = Rgb[i + c];
                    sum += v;
                    sumSquares += v * v;
                    count++;
                }
            }
        }

        if (count == 0)
        {
            return 0;
        }

        var mean = sum / count;
        return Math.Sqrt(Math.Max(0, sumSquares / count - mean * mean));
    }
}