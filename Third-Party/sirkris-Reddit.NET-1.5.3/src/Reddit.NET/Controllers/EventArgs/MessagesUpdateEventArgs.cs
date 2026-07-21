using System.Collections.Generic;
using Reddit.Things;

namespace Reddit.Controllers.EventArgs
{
    public class MessagesUpdateEventArgs
    {
        public List<Message> OldMessages { get; set; }
        public List<Message> NewMessages { get; set; }
        public List<Message> Added { get; set; }
        public List<Message> Removed { get; set; }
    }
}
