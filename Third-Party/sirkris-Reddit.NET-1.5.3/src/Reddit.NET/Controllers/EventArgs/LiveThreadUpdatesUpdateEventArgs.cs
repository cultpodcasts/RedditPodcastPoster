using System.Collections.Generic;
using Reddit.Things;

namespace Reddit.Controllers.EventArgs
{
    public class LiveThreadUpdatesUpdateEventArgs
    {
        public List<LiveUpdate> OldUpdates { get; set; }
        public List<LiveUpdate> NewUpdates { get; set; }
        public List<LiveUpdate> Added { get; set; }
        public List<LiveUpdate> Removed { get; set; }
    }
}
