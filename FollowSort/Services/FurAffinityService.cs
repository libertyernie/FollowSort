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
                .Where(x => x.SourceSite == SourceSite.FurAffinity)
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
            if (a.SourceSite != SourceSite.FurAffinity)
                throw new Exception($"Artist {artistId} is not a FurAffinity user");

            await Refresh(context, a, save);
        }

        private interface IFASubmission
        {
            string Id { get; }
            string Title { get; }
            string Thumbnail { get; }
            string Link { get; }
        }

        private class FASubmission : IFASubmission
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Thumbnail { get; set; }
            public string Link { get; set; }
        }

        private class FAFullSubmission : IFASubmission
        {
            public string Title { get; set; }
            public string Thumbnail { get; set; }
            public string Link { get; set; }
            public DateTimeOffset Posted_at { get; set; }
            public string Rating { get; set; }
            public IEnumerable<string> Keywords { get; set; }

            public string Id => Link.Split('/').Where(s => s != "").Last();
        }

        private class FAJournal
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Link { get; set; }
            public DateTimeOffset Posted_at { get; set; }
        }

        private static async Task<IList<IFASubmission>> GetSubmissionsAsync(Artist a)
        {
            var list = new List<IFASubmission>();

            bool newUser = a.LastCheckedSourceSiteId == null;
            long prevId = long.TryParse(a.LastCheckedSourceSiteId, out long tmp1) ? tmp1 : 0;
            for (int i = 1; i <= (newUser ? 1 : 3); i++)
            {
                var req1 = WebRequest.CreateHttp($"https://faexport.boothale.net/user/{WebUtility.UrlEncode(a.Name)}/gallery.json?full=1&page={i}");
                using (var resp1 = await req1.GetResponseAsync())
                using (var sr1 = new StreamReader(resp1.GetResponseStream()))
                {
                    var array = JsonConvert.DeserializeObject<IEnumerable<FASubmission>>(await sr1.ReadToEndAsync());
                    foreach (var o in array)
                    {
                        if (long.TryParse(o.Id, out long tmp2) && tmp2 <= prevId)
                            return list;

                        if (!a.Nsfw || a.TagFilter.Any())
                        {
                            // We need more information.
                            var req2 = WebRequest.CreateHttp($"https://faexport.boothale.net/submission/{o.Id}.json");
                            using (var resp2 = await req2.GetResponseAsync())
                            using (var sr2 = new StreamReader(resp2.GetResponseStream()))
                            {
                                list.Add(JsonConvert.DeserializeObject<FAFullSubmission>(await sr2.ReadToEndAsync()));
                            }
                        }
                        else
                        {
                            // We have enough information (we don't have post date, but we can just make one up.)
                            list.Add(o);
                        }
                    }
                }
            }

            return list;
        }

        private static async Task<IList<FAJournal>> GetJournalsAsync(Artist a)
        {
            var list = new List<FAJournal>();

            bool newUser = a.LastCheckedSourceSiteId == null;
            long prevId = long.TryParse(a.LastCheckedSourceSiteId, out long tmp1) ? tmp1 : 0;
            for (int i = 1; i <= (newUser ? 1 : 3); i++)
            {
                var req1 = WebRequest.CreateHttp($"https://faexport.boothale.net/user/{WebUtility.UrlEncode(a.Name)}/journals.json?full=1&page={i}");
                using (var resp1 = await req1.GetResponseAsync())
                using (var sr1 = new StreamReader(resp1.GetResponseStream()))
                {
                    var array = JsonConvert.DeserializeObject<IEnumerable<FAJournal>>(await sr1.ReadToEndAsync());
                    foreach (var o in array)
                    {
                        if (o.Posted_at <= a.LastChecked) return list;

                        list.Add(o);
                    }
                }
            }

            return list;
        }

        private static IEnumerable<Notification> FilterSubmissions(Artist a, IEnumerable<IFASubmission> submissions)
        {
            var now = DateTime.UtcNow;
            foreach (var s in submissions)
            {
                var details = s as FAFullSubmission;
                if (!a.Nsfw)
                    if (details?.Rating != "General")
                        continue;
                if (a.TagFilter.Any())
                    if (details?.Keywords?.Intersect(a.TagFilter, StringComparer.InvariantCultureIgnoreCase)?.Any() != true)
                        continue;
                yield return new Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = s.Id,
                    ArtistName = a.Name,
                    Url = s.Link,
                    TextPost = false,
                    ThumbnailUrl = s.Thumbnail,
                    Name = s.Title,
                    PostDate = details?.Posted_at ?? now
                };
            }
        }

        public async Task Refresh(ApplicationDbContext context,
            Artist a,
            bool save = false)
        {
            var submissions = await GetSubmissionsAsync(a);
            foreach (var n in FilterSubmissions(a, submissions))
            {
                context.Notifications.Add(n);
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
