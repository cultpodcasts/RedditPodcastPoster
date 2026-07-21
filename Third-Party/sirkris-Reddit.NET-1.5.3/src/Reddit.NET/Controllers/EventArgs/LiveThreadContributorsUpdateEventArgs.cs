using System.Collections.Generic;
using Reddit.Things;

namespace Reddit.Controllers.EventArgs
{
    public class LiveThreadContributorsUpdateEventArgs
    {
        public List<UserListContainer> OldContributors { get; set; }
        public List<UserListContainer> NewContributors { get; set; }
        public List<UserListContainer> Added { get; set; }
        public List<UserListContainer> Removed { get; set; }
    }
}
