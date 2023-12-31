﻿using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface ICachedSubjectRepository
{
    Task<IEnumerable<Subject>> GetAll();
}