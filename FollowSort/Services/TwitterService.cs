using FollowSort.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace FollowSort.Services
{
    public interface ITwitterService : IConsumerCredentials
    {
        Task RefreshAll(ApplicationDbContext context,
            ITwitterCredentials creds,
            string userId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            ITwitterCredentials creds,
            string userId,
            Guid artistId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            ITwitterCredentials creds,
            Artist a,
            bool save = false);

        Task<string> GetAvatarUrlAsync(ITwitterCredentials creds, string screenName);
    }

    public class TwitterService : ConsumerCredentials, ITwitterService
    {
        public TwitterService(string consumerKey, string consumerSecret) : base(consumerKey, consumerSecret) { }

        public async Task RefreshAll(ApplicationDbContext context,
            ITwitterCredentials creds,
            string userId,
            bool save = false)
        {
            var artists = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.SourceSite == SourceSite.Twitter)
                .ToListAsync();

            await Task.WhenAll(artists.Select(a => Refresh(context, creds, a)));
            if (save) await context.SaveChangesAsync();
        }

        public async Task Refresh(ApplicationDbContext context,
            ITwitterCredentials creds,
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
            if (a.SourceSite != SourceSite.Twitter)
                throw new Exception($"Artist {artistId} is not a Twitter user");

            await Refresh(context, creds, a, save);
        }

        public async Task Refresh(ApplicationDbContext context,
            ITwitterCredentials creds,
            Artist a,
            bool save = false)
        {
            if (creds.AccessToken == null || creds.AccessTokenSecret == null)
            {
                throw new Exception("Cannot get new notifications from Twitter (not logged in)");
            }

            var tweets = await Auth.ExecuteOperationWithCredentials(creds, () => TimelineAsync.GetUserTimeline(new UserIdentifier(a.Name), new UserTimelineParameters
            {
                SinceId = long.TryParse(a.LastCheckedSourceSiteId, out long l) ? l : 20,
                IncludeEntities = true,
                MaximumNumberOfTweetsToRetrieve = a.LastCheckedSourceSiteId == null ? 20 : 200,
                ExcludeReplies = true
            }));

            if (tweets == null)
            {
                var ex = ExceptionHandler.GetLastException();
                throw new Exception(ex?.TwitterDescription ?? "Could not get tweets", ex as Exception);
            }

            foreach (var t in tweets)
            {
                if (!a.Nsfw && t.PossiblySensitive) continue;

                if (a.TagFilter.Any())
                {
                    if (!t.Entities.Hashtags.Select(h => h.Text.Replace("#", "")).Intersect(a.TagFilter, StringComparer.InvariantCultureIgnoreCase).Any())
                    {
                        continue;
                    }
                }

                var photos = t.Entities.Medias.Where(m => m.MediaType == "photo");

                if (photos.Any() && t.IsRetweet && !a.IncludeRepostedPhotos) continue;
                if (!photos.Any() && !t.IsRetweet && !a.IncludeNonPhotos) continue;
                if (!photos.Any() && t.IsRetweet && !a.IncludeRepostedNonPhotos) continue;

                System.Diagnostics.Debug.WriteLine($"Adding Twitter post {t.IdStr} from {(t.RetweetedTweet?.CreatedBy ?? t.CreatedBy).ScreenName}");
                if (photos.Any())
                {
                    foreach (var p in photos)
                    {
                        context.Notifications.Add(new Notification
                        {
                            UserId = a.UserId,
                            SourceSite = a.SourceSite,
                            SourceSiteId = t.IdStr,
                            ArtistName = (t.RetweetedTweet?.CreatedBy ?? t.CreatedBy).ScreenName,
                            RepostedByArtistName = t.CreatedBy.ScreenName,
                            Url = t.Url,
                            TextPost = false,
                            Repost = t.IsRetweet,
                            ThumbnailUrl = p.MediaURLHttps,
                            Name = t.RetweetedTweet?.Text ?? t.Text,
                            PostDate = t.CreatedAt
                        });
                    }
                }
                else
                {
                    context.Notifications.Add(new Notification
                    {
                        UserId = a.UserId,
                        SourceSite = a.SourceSite,
                        SourceSiteId = t.IdStr,
                        ArtistName = (t.RetweetedTweet?.CreatedBy ?? t.CreatedBy).ScreenName,
                        RepostedByArtistName = t.CreatedBy.ScreenName,
                        Url = t.Url,
                        TextPost = true,
                        Repost = t.IsRetweet,
                        ThumbnailUrl = null,
                        Name = t.RetweetedTweet?.Text ?? t.Text,
                        PostDate = t.CreatedAt
                    });
                }
            }

            a.LastChecked = DateTimeOffset.UtcNow;
            if (tweets.Any())
            {
                a.LastCheckedSourceSiteId = tweets.Select(t => t.Id).Max().ToString();
            }

            if (save) await context.SaveChangesAsync();
        }

        public async Task<string> GetAvatarUrlAsync(ITwitterCredentials creds, string screenName)
        {
            var user = await Auth.ExecuteOperationWithCredentials(creds, () => UserAsync.GetUserFromScreenName(screenName));
            return user?.ProfileImageUrlHttps;
        }
    }
}
