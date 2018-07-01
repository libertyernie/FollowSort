using DeviantartApi.Objects;
using FollowSort.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FollowSort.Services
{
    public interface IDeviantArtService
    {
        Task RefreshAll(ApplicationDbContext context,
            string userId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            string userId,
            Guid artistId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            Artist a,
            bool save = false);
    }

    public class DeviantArtService : IDeviantArtService
    {
        private static SemaphoreSlim UpdateAccessTokenLock = new SemaphoreSlim(1, 1);

        private async Task UpdateAccessTokenAsync()
        {
            if (DeviantartApi.Requester.AccessTokenExpire < DateTime.UtcNow) return;

            await UpdateAccessTokenLock.WaitAsync();
            try
            {
                if (DeviantartApi.Requester.AccessTokenExpire < DateTime.UtcNow)
                {
                    // Access token is expired - get a new one
                    DateTime now = DateTime.UtcNow;
                    var req = WebRequest.CreateHttp("https://www.deviantart.com/oauth2/token");
                    req.Method = "POST";
                    req.ContentType = "application/x-www-form-urlencoded";
                    using (var sw = new StreamWriter(await req.GetRequestStreamAsync()))
                    {
                        await sw.WriteAsync($"client_id={DeviantartApi.Requester.AppClientId}&");
                        await sw.WriteAsync($"client_secret={DeviantartApi.Requester.AppSecret}&");
                        await sw.WriteAsync($"grant_type=client_credentials");
                    }
                    using (var resp = await req.GetResponseAsync())
                    using (var sr = new StreamReader(resp.GetResponseStream()))
                    {
                        var tr = JsonConvert.DeserializeAnonymousType(await sr.ReadToEndAsync(), new
                        {
                            expires_in = 0,
                            status = "",
                            access_token = "",
                            token_type = ""
                        });
                        if (tr.status != "success") throw new Exception("OAuth client credentials request not successful");
                        if (tr.token_type != "Bearer") throw new Exception("Token recieved was not a bearer token");
                        DeviantartApi.Requester.AccessToken = tr.access_token;
                        DeviantartApi.Requester.AccessTokenExpire = now.AddSeconds(tr.expires_in);
                        DeviantartApi.Requester.AutoAccessTokenCheckingDisabled = true;
                    }
                }
            }
            finally
            {
                UpdateAccessTokenLock.Release();
            }
        }

        public async Task RefreshAll(ApplicationDbContext context,
            string userId,
            bool save = false)
        {
            var artists = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.SourceSite == SourceSite.DeviantArt)
                .ToListAsync();

            await Task.WhenAll(artists.Select(a => Refresh(context, a)));
            if (save) await context.SaveChangesAsync();
        }

        public async Task Refresh(ApplicationDbContext context,
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

            await Refresh(context, a, save);
        }
        
        private static async Task<IList<Deviation>> GetPosts(Artist a)
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

        private static async Task<IList<Deviation>> GetJournals(Artist a)
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
                    if (timestamp <= a.LastChecked) return posts;
                    if (timestamp <= DateTime.UtcNow.AddDays(-28)) return posts;
                    posts.Add(d);
                }
            }
            return posts;
        }

        public async Task Refresh(ApplicationDbContext context,
            Artist a,
            bool save = false)
        {
            IList<Deviation> posts, journals;

            await UpdateAccessTokenAsync();
            posts = await GetPosts(a);

            await UpdateAccessTokenAsync();
            journals = await GetJournals(a);

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
                    ImageUrl = thumbnail,
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
                    ImageUrl = null,
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
    }
}
