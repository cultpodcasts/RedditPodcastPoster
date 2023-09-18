using API.Models;

namespace API.Dtos
{
    public class HomePageModel
    {
        public IEnumerable<PodcastResult> RecentEpisodes { get; set; }
        public int? EpisodeCount { get; set; }
    }
}
