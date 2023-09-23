using API.Models;

namespace API.Dtos
{
    public class HomePageModel
    {
        public IEnumerable<RecentEpisode> RecentEpisodes { get; set; }
        public int? EpisodeCount { get; set; }
        public TimeSpan TotalDuration { get; set; }
    }
}
