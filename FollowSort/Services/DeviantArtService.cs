using FollowSort.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

        public class Response<T>
        {
            public bool has_more;
            public int? next_offset;
            public IList<T> results;
        }

        public class Deviation
        {
            public Guid deviationid;
            public string url;
            public string title;
            public long published_time;
            public IList<Thumbnail> thumbnails;
            public bool is_mature;
        }

        public class Thumbnail
        {
            public string url;
            public int width, height;
            public bool transparency;
        }

        private static async Task<IList<Deviation>> GetPosts(string token, Artist a)
        {
            var posts = new List<Deviation>();
            int offset = 0;
            for (int i = 0; i < (a.LastChecked == null ? 1 : 10); i++)
            {
                var galleryRequest = WebRequest.CreateHttp($"https://www.deviantart.com/api/v1/oauth2/gallery/all?username={WebUtility.UrlEncode(a.Name)}&offset={offset}&limit=24&access_token={token}");
                using (var resp = await galleryRequest.GetResponseAsync())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    var obj = JsonConvert.DeserializeObject<Response<Deviation>>(await sr.ReadToEndAsync());
                    foreach (var d in obj.results)
                    {
                        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(d.published_time);
                        if (timestamp <= a.LastChecked)
                        {
                            return posts;
                        }
                        posts.Add(d);
                    }
                }
            }
            return posts;
        }

        private static async Task<IList<Deviation>> GetJournals(string token, Artist a)
        {
            var posts = new List<Deviation>();
            int offset = 0;
            for (int i = 0; i < (a.LastChecked == null ? 1 : 20); i++)
            {
                var journalRequest = WebRequest.CreateHttp($"https://www.deviantart.com/api/v1/oauth2/browse/user/journals?username={WebUtility.UrlEncode(a.Name)}&offset={offset}&limit=10&access_token={token}");
                using (var resp = await journalRequest.GetResponseAsync())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    var obj = JsonConvert.DeserializeObject<Response<Deviation>>(await sr.ReadToEndAsync());
                    foreach (var d in obj.results)
                    {
                        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(d.published_time);
                        if (timestamp <= a.LastChecked)
                        {
                            return posts;
                        }
                        posts.Add(d);
                    }
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

            var posts = await GetPosts(token, a);

            foreach (var p in posts)
            {
                if (a.TagFilter.Any())
                {
                    throw new NotImplementedException("Cannot filter DeviantArt submissions by tags (not implemented)");
                }

                if (!a.Nsfw && p.is_mature) continue;

                System.Diagnostics.Debug.WriteLine($"Adding DeviantArt post {p.deviationid} from {a.Name}");
                context.Notifications.Add(new Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = p.deviationid.ToString(),
                    ArtistName = a.Name,
                    Url = p.url,
                    TextPost = false,
                    Repost = false,
                    ThumbnailUrl = p.thumbnails.First()?.url,
                    Name = p.title,
                    PostDate = DateTimeOffset.FromUnixTimeSeconds(p.published_time)
                });
            }

            var journals = await GetJournals(token, a);

            foreach (var p in journals)
            {
                if (a.TagFilter.Any())
                {
                    throw new NotImplementedException("Cannot filter DeviantArt journals by tags (not implemented)");
                }

                if (!a.Nsfw && p.is_mature) continue;

                System.Diagnostics.Debug.WriteLine($"Adding DeviantArt journal {p.deviationid} from {a.Name}");
                context.Notifications.Add(new Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = p.deviationid.ToString(),
                    ArtistName = a.Name,
                    Url = p.url,
                    TextPost = true,
                    Repost = false,
                    ThumbnailUrl = null,
                    Name = p.title,
                    PostDate = DateTimeOffset.FromUnixTimeSeconds(p.published_time)
                });
            }

            a.LastChecked = DateTimeOffset.UtcNow;
            if (posts.Any())
            {
                a.LastCheckedSourceSiteId = posts
                    .OrderByDescending(p => p.published_time)
                    .Select(p => p.deviationid.ToString())
                    .First();
            }

            if (save) await context.SaveChangesAsync();
        }

        public async Task<string> GetAvatarUrlAsync(string token, string screenName)
        {
            var galleryRequest = WebRequest.CreateHttp($"https://www.deviantart.com/api/v1/oauth2/user/whois?usernames={WebUtility.UrlEncode(screenName)}&access_token={token}");
            try
            {
                using (var resp = await galleryRequest.GetResponseAsync())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    var obj = JsonConvert.DeserializeAnonymousType(await sr.ReadToEndAsync(), new
                    {
                        results = new[]
                        {
                        new
                        {
                            usericon = ""
                        }
                    }
                    });
                    return obj.results[0].usericon;
                }
            } catch (WebException ex)
            {
                using (var resp = ex.Response)
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    string html = await sr.ReadToEndAsync();
                    throw;
                }
            }
        }
    }
}
