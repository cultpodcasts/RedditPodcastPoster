namespace RedditPodcastPoster.Text.Models;

/// <summary>
/// Mac-prefix surnames that should be recapitalised after title-case
/// (e.g. Macewan → MacEwan). Words not in this set are left alone
/// (Machine, Machination, Macron, …).
/// </summary>
public static class MacSurnames
{
    /// <summary>
    /// Lowercase full surnames (including the mac prefix). Matched case-insensitively
    /// against title tokens after <c>ToTitleCase</c>.
    /// </summary>
    public static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "macadam", "macalister", "macallister", "macarthur", "macaulay", "macbride",
        "maccallum", "maccarthy", "maccormack", "maccrimmon", "macculloch",
        "macdermott", "macdiarmid", "macdonald", "macdonnell", "macdougall",
        "macdowell", "macduff", "maceachen", "macewan", "macewen",
        "macfarlane", "macgill", "macgowan", "macgregor", "macguinness", "macguire",
        "macinnes", "macintyre", "maciver", "mackay", "mackenzie", "mackinnon",
        "maclachlan", "maclaren", "maclean", "maclellan", "macleod",
        "macmillan", "macnab", "macnair", "macnamara", "macnaughton",
        "macneil", "macneill", "macpherson", "macquarrie", "macqueen",
        "macrae", "mactavish", "macwilliams"
    };
}
