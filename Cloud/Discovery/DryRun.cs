namespace Discovery;

internal static class DryRun
{
    // static readonly (not const) so flipping the toggle doesn't leave provably unreachable code (CS0162).
    public static readonly bool IsDiscoverDryRun = false;
}