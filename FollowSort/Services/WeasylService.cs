using FollowSort.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WeasylLib;

namespace FollowSort.Services
{
    public interface IWeasylService
    {
        Task RefreshAll(ApplicationDbContext context,
            string apiKey,
            string userId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            string apiKey,
            string userId,
            Guid artistId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            string apiKey,
            Artist a,
            bool save = false);

        Task<string> GetAvatarUrlAsync(string apiKey, string screenName);
    }

    public class WeasylService : IWeasylService
    {
        public async Task RefreshAll(ApplicationDbContext context,
            string apiKey,
            string userId,
            bool save = false)
        {
            var artists = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.SourceSite == SourceSite.Weasyl)
                .ToListAsync();

            await Task.WhenAll(artists.Select(a => Refresh(context, apiKey, a)));
            if (save) await context.SaveChangesAsync();
        }

        public async Task Refresh(ApplicationDbContext context,
            string apiKey,
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
            if (a.SourceSite != SourceSite.Weasyl)
                throw new Exception($"Artist {artistId} is not a Weasyl user");

            await Refresh(context, apiKey, a, save);
        }

        public async Task Refresh(ApplicationDbContext context,
            string apiKey,
            Artist a,
            bool save = false)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("Cannot get new notifications from Weasyl (not logged in)");
            }

            var client = new WeasylClient(apiKey);
            var gallery = await client.GetUserGalleryAsync(a.Name, a.LastChecked.UtcDateTime, count: 20);
            var submissions = new List<WeasylGallerySubmission>(gallery.submissions);
            if (a.LastCheckedSourceSiteId != null && gallery.submissions.Any() && gallery.nextid != null)
            {
                // This is not a new user - try to get more submissions
                for (int i = 0; i < 2; i++)
                {
                    gallery = await client.GetUserGalleryAsync(a.Name, a.LastChecked.UtcDateTime, count: 100, nextid: gallery.nextid);
                    submissions.AddRange(gallery.submissions);
                }
            }

            foreach (var s in submissions)
            {
                if (!a.Nsfw && s.rating != "general") continue;

                if (a.TagFilter.Any())
                {
                    var details = await client.GetSubmissionAsync(s.submitid);
                    if (!details.tags.Intersect(a.TagFilter, StringComparer.InvariantCultureIgnoreCase).Any())
                    {
                        continue;
                    }
                }

                string thumbnailUrl = s.media.thumbnail.Select(t => t.url).FirstOrDefault();
                
                System.Diagnostics.Debug.WriteLine($"Adding Weasyl post {s.submitid} from {s.owner}");
                context.Notifications.Add(new Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    SourceSiteId = s.submitid.ToString(),
                    ArtistName = s.owner,
                    Url = $"https://www.weasyl.com/~{WebUtility.UrlEncode(s.owner)}/submissions/{s.submitid}",
                    TextPost = false,
                    ThumbnailUrl = thumbnailUrl,
                    Name = s.title,
                    PostDate = s.posted_at
                });
            }

            a.LastChecked = DateTimeOffset.UtcNow;
            if (submissions.Any())
            {
                a.LastCheckedSourceSiteId = submissions.Select(t => t.submitid).Max().ToString();
            }

            if (save) await context.SaveChangesAsync();
        }

        public async Task<string> GetAvatarUrlAsync(string apiKey, string screenName)
        {
            var client = new WeasylClient(apiKey);
            return await client.GetAvatarUrlAsync(screenName);
        }
    }
}
