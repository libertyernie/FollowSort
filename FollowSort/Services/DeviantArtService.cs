using DeviantartApi.Objects;
using FollowSort.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FollowSort.Services
{
    public interface IDeviantArtService
    {
        Task RefreshAll(ApplicationDbContext context,
            string token,
            string userId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            string token,
            string userId,
            Guid artistId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            string token,
            Artist a,
            bool save = false);

        Task<string> GetAvatarUrlAsync(string token, string screenName);
    }

    public class DeviantArtService : IDeviantArtService
    {
        private static SemaphoreSlim LibraryLock = new SemaphoreSlim(1, 1);

        public async Task RefreshAll(ApplicationDbContext context,
            string token,
            string userId,
            bool save = false)
        {
            var artists = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.SourceSite == SourceSite.DeviantArt)
                .ToListAsync();

            await Task.WhenAll(artists.Select(a => Refresh(context, token, a)));
            if (save) await context.SaveChangesAsync();
        }

        public async Task Refresh(ApplicationDbContext context,
            string token,
            string userId,
            Guid artistId,
            bool save = false)
        {
            Artist a = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.Id == artistId)
                .SingleOrDefaultAsync();

            if (a == null)
                throw new Exception($"Artist {artistId} not found");
            if (a.SourceSite != SourceSite.DeviantArt)
                throw new Exception($"Artist {artistId} is not a DeviantArt user");

            await Refresh(context, token, a, save);
        }
        
        private static async Task<IList<Deviation>> GetPosts(string token, Artist a)
        {
            var posts = new List<Deviation>();
            var request = new DeviantartApi.Requests.Gallery.AllRequest
            {
                Limit = 24,
                Username = a.Name
            };
            for (int i = 0; i < (a.LastCheckedSourceSiteId == null ? 1 : 10); i++)
            {
                var resp = await request.GetNextPageAsync();
                foreach (var d in resp.Result.Results)
                {
                    DateTimeOffset timestamp = d.PublishedTime ?? DateTimeOffset.Now;
                    if (timestamp <= a.LastChecked)
                    {
                        return posts;
                    }
                    posts.Add(d);
                }
            }
            return posts;
        }

        private static async Task<IList<Deviation>> GetJournals(string token, Artist a)
        {
            var posts = new List<Deviation>();
            var request = new DeviantartApi.Requests.Browse.User.JournalsRequest(a.Name)
            {
                Limit = 10
            };
            for (int i = 0; i < (a.LastCheckedSourceSiteId == null ? 1 : 10); i++)
            {
                var resp = await request.GetNextPageAsync();
                foreach (var d in resp.Result.Results)
                {
                    DateTimeOffset timestamp = d.PublishedTime ?? DateTimeOffset.Now;
                    if (timestamp <= a.LastChecked)
                    {
                        return posts;
                    }
                    posts.Add(d);
                }
            }
            return posts;
        }

        public async Task Refresh(ApplicationDbContext context,
            string token,
            Artist a,
            bool save = false)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new Exception("Cannot get new notifications from DeviantArt (token null or empty)");
            }

            IList<Deviation> posts, journals;

            await LibraryLock.WaitAsync();

            try
            {
                DeviantartApi.Requester.AccessToken = token;

                posts = await GetPosts(token, a);
                journals = await GetJournals(token, a);
            }
            finally
            {
                LibraryLock.Release();
            }

            foreach (var p in posts)
            {
                if (a.TagFilter.Any())
                {
                    throw new NotImplementedException("Cannot filter DeviantArt submissions by tags (not implemented)");
                }

                if (!a.Nsfw && p.IsMature == true) continue;

                System.Diagnostics.Debug.WriteLine($"Adding DeviantArt post {p.DeviationId} from {a.Name}");
                string thumbnail = p.Thumbs.Select(t => t.Src).FirstOrDefault();
                context.Notifications.Add(new Data.Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = p.DeviationId,
                    ArtistName = a.Name,
                    Url = p.Url.OriginalString,
                    TextPost = !p.Thumbs.Any(),
                    Repost = false,
                    ThumbnailUrl = thumbnail,
                    Name = p.Title,
                    PostDate = p.PublishedTime ?? DateTime.UtcNow
                });
            }

            foreach (var p in journals)
            {
                if (a.TagFilter.Any())
                {
                    throw new NotImplementedException("Cannot filter DeviantArt journals by tags (not implemented)");
                }

                if (!a.Nsfw && p.IsMature == true) continue;

                System.Diagnostics.Debug.WriteLine($"Adding DeviantArt journal {p.DeviationId} from {a.Name}");
                context.Notifications.Add(new Data.Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = p.DeviationId,
                    ArtistName = a.Name,
                    Url = p.Url.OriginalString,
                    TextPost = true,
                    Repost = false,
                    ThumbnailUrl = null,
                    Name = p.Title,
                    PostDate = p.PublishedTime ?? DateTime.UtcNow
                });
            }

            a.LastChecked = DateTimeOffset.UtcNow;
            if (posts.Any())
            {
                a.LastCheckedSourceSiteId = posts
                    .OrderByDescending(p => p.PublishedTime)
                    .Select(p => p.DeviationId)
                    .First();
            }

            if (save) await context.SaveChangesAsync();
        }

        public async Task<string> GetAvatarUrlAsync(string token, string screenName)
        {
            await LibraryLock.WaitAsync();

            try
            {
                DeviantartApi.Requester.AccessToken = token;

                var request = new DeviantartApi.Requests.User.WhoIsRequest(new[] { screenName });
                var user = await request.ExecuteAsync();
                return user.Result.Results.First().UserIconUrl.OriginalString;
            }
            finally
            {
                LibraryLock.Release();
            }
        }
    }
}
