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
    public interface IFurAffinityService
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

        Task<string> GetAvatarUrlAsync(string screenName);
    }

    public class FurAffinityService : IFurAffinityService
    {
        public async Task RefreshAll(ApplicationDbContext context,
            string userId,
            bool save = false)
        {
            var artists = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.SourceSite == SourceSite.FurAffinity || x.SourceSite == SourceSite.FurAffinity_Favorites)
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
            if (a.SourceSite != SourceSite.FurAffinity && a.SourceSite != SourceSite.FurAffinity_Favorites)
                throw new Exception($"Artist {artistId} is not a FurAffinity user");

            await Refresh(context, a, save);
        }
        
        private class FASubmission
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Thumbnail { get; set; }
            public string Link { get; set; }
        }
        
        private class FAJournal
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Link { get; set; }
            public DateTimeOffset Posted_at { get; set; }
        }

        private static async Task<IList<FASubmission>> GetSubmissionsAsync(Artist a)
        {
            var list = new List<FASubmission>();

            bool newUser = a.LastCheckedSourceSiteId == null;
            for (int i = 1; i <= (newUser ? 1 : 3); i++)
            {
                string folder = a.SourceSite == SourceSite.FurAffinity_Favorites
                    ? "favorites"
                    : "gallery";
                var req1 = WebRequest.CreateHttp($"https://faexport.boothale.net/user/{WebUtility.UrlEncode(a.Name)}/{folder}.json?full=1&page={i}&sfw={(a.Nsfw?0:1)}");
                using (var resp1 = await req1.GetResponseAsync())
                using (var sr1 = new StreamReader(resp1.GetResponseStream()))
                {
                    var array = JsonConvert.DeserializeObject<IEnumerable<FASubmission>>(await sr1.ReadToEndAsync());
                    foreach (var o in array)
                    {
                        if (o.Id == a.LastCheckedSourceSiteId)
                            return list;

                        list.Add(o);
                    }
                }
            }

            return list;
        }

        private static async Task<IList<FAJournal>> GetJournalsAsync(Artist a)
        {
            var list = new List<FAJournal>();

            if (a.SourceSite != SourceSite.FurAffinity_Favorites)
            {
                bool newUser = a.LastCheckedSourceSiteId == null;
                for (int i = 1; i <= (newUser ? 1 : 3); i++)
                {
                    var req1 = WebRequest.CreateHttp($"https://faexport.boothale.net/user/{WebUtility.UrlEncode(a.Name)}/journals.json?full=1&page={i}&sfw={(a.Nsfw?0:1)}");
                    using (var resp1 = await req1.GetResponseAsync())
                    using (var sr1 = new StreamReader(resp1.GetResponseStream()))
                    {
                        var array = JsonConvert.DeserializeObject<IEnumerable<FAJournal>>(await sr1.ReadToEndAsync());
                        foreach (var o in array)
                        {
                            if (o.Posted_at <= a.LastChecked) return list;
                            if (o.Posted_at <= DateTime.UtcNow.AddDays(-28)) return list;

                            list.Add(o);
                        }
                    }
                }
            }

            return list;
        }
        
        public async Task Refresh(ApplicationDbContext context,
            Artist a,
            bool save = false)
        {
            bool fav = a.SourceSite == SourceSite.FurAffinity_Favorites;

            var now = DateTime.UtcNow;
            var submissions = await GetSubmissionsAsync(a);
            foreach (var s in submissions)
            {
                context.Notifications.Add(new Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = s.Id,
                    ArtistName = fav ? null : a.Name,
                    Repost = fav,
                    RepostedByArtistName = fav ? a.Name : null,
                    Url = s.Link,
                    TextPost = false,
                    ThumbnailUrl = s.Thumbnail,
                    Name = s.Title,
                    PostDate = now
                });
            }

            if (a.IncludeNonPhotos)
            {
                var journals = await GetJournalsAsync(a);
                foreach (var j in journals)
                {
                    context.Add(new Notification
                    {
                        UserId = a.UserId,
                        SourceSite = a.SourceSite,
                        SourceSiteId = j.Id,
                        ArtistName = a.Name,
                        Url = j.Link,
                        TextPost = true,
                        Name = j.Title,
                        PostDate = j.Posted_at
                    });
                }
            }

            a.LastChecked = DateTimeOffset.UtcNow;
            if (submissions.Any())
            {
                a.LastCheckedSourceSiteId = submissions.Select(t => t.Id).First();
            }

            if (save) await context.SaveChangesAsync();
        }

        public async Task<string> GetAvatarUrlAsync(string screenName)
        {
            var req = WebRequest.CreateHttp($"https://faexport.boothale.net/user/{WebUtility.UrlEncode(screenName)}.json");
            using (var resp = await req.GetResponseAsync())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                var user = JsonConvert.DeserializeAnonymousType(
                    await sr.ReadToEndAsync(),
                    new { avatar = "" });
                return user.avatar;
            }
        }
    }
}
