using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DontPanic.TumblrSharp.Client;
using FollowSort.Data;
using FollowSort.Models;
using FollowSort.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace FollowSort.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConsumerCredentials _twitterConsumerCredentials;
        private readonly IFollowSortTumblrClientFactory _tumblrFactory;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConsumerCredentials twitterConsumerCredentials,
            IFollowSortTumblrClientFactory tumblrFactory)
        {
            _context = context;
            _userManager = userManager;
            _twitterConsumerCredentials = twitterConsumerCredentials;
            _tumblrFactory = tumblrFactory;
        }

        public async Task<IActionResult> Index()
        {
            string userId = _userManager.GetUserId(User);
            var q = _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.PostDate);
            return View(await q.ToListAsync());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(IFormCollection data)
        {
            foreach (var s in data?.Keys ?? new string[0])
            {
                if (s.StartsWith("chk") && Guid.TryParse(s.Substring(3), out Guid g))
                {
                    _context.RemoveRange(_context.Notifications.Where(n => n.Id == g));
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Refresh()
        {
            string userId = _userManager.GetUserId(User);
            var artists = await _context.Artists.Where(a => a.UserId == userId).ToListAsync();
            await Task.WhenAll(RefreshTwitter(artists), RefreshTumblr(artists));
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task RefreshTwitter(IEnumerable<Artist> artists)
        {
            var user = await _userManager.GetUserAsync(User);
            var creds = new TwitterCredentials(
                _twitterConsumerCredentials.ConsumerKey,
                _twitterConsumerCredentials.ConsumerSecret,
                await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token"),
                await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token_secret")
            );

            if (creds.AccessToken == null || creds.AccessTokenSecret == null) {
                throw new Exception("Cannot get new notifications from Twitter (not logged in)");
            }

            foreach (var a in artists)
            {
                if (a.SourceSite != SourceSite.Twitter) continue;

                var tweets = await Auth.ExecuteOperationWithCredentials(creds, () => TimelineAsync.GetUserTimeline(new UserIdentifier(a.Name), new UserTimelineParameters
                {
                    SinceId = long.TryParse(a.LastCheckedSourceSiteId, out long l) ? l : 20,
                    IncludeEntities = true,
                    MaximumNumberOfTweetsToRetrieve = a.LastCheckedSourceSiteId == null ? 20 : 200
                }));

                if (tweets == null)
                {
                    var ex = ExceptionHandler.GetLastException();
                    throw new Exception(ex?.TwitterDescription ?? "Could not get tweets", ex as Exception);
                }

                foreach (var t in tweets)
                {
                    var photos = t.Entities.Medias.Where(m => m.MediaType == "photo");

                    if (photos.Any() && t.IsRetweet && !a.IncludeRepostedPhotos) continue;
                    if (!photos.Any() && !t.IsRetweet && !a.IncludeNonPhotos) continue;
                    if (!photos.Any() && t.IsRetweet && !a.IncludeRepostedNonPhotos) continue;

                    if (photos.Any())
                    {
                        foreach (var p in photos)
                        {
                            _context.Notifications.Add(new Notification
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
                        _context.Notifications.Add(new Notification
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
            }
        }

        private static async Task<IList<BasePost>> GetPosts(TumblrClient client, Artist a)
        {
            var posts = new List<BasePost>();
            for (int i = 0; i < (a.LastCheckedSourceSiteId == null ? 1 : 10); i++)
            {
                var x = await client.GetPostsAsync(
                    a.Name,
                    i * 20,
                    20,
                    includeReblogInfo: true,
                    filter: DontPanic.TumblrSharp.PostFilter.Text);
                foreach (var p in x.Result)
                {
                    if (p.Timestamp <= a.LastChecked)
                    {
                        return posts;
                    }
                    posts.Add(p);
                }
            }
            return posts;
        }

        private async Task RefreshTumblr(IEnumerable<Artist> artists)
        {
            var user = await _userManager.GetUserAsync(User);

            var access_token = await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token");
            var access_token_secret = await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token_secret");
            if (access_token == null || access_token_secret == null) {
                throw new Exception("Cannot get new notifications from Tumblr (not logged in)");
            }

            using (var client = _tumblrFactory.Create(
                access_token,
                access_token_secret))
            {
                foreach (var a in artists)
                {
                    if (a.SourceSite != SourceSite.Tumblr) continue;

                    var posts = await GetPosts(client, a);
                    
                    foreach (var p in posts)
                    {
                        string title = (p as TextPost)?.Title?.NullIfEmpty()
                                    ?? (p as TextPost)?.Body?.NullIfEmpty()
                                    ?? (p as QuotePost)?.Text?.NullIfEmpty()
                                    ?? (p as LinkPost)?.Title?.NullIfEmpty()
                                    ?? (p as ChatPost)?.Title?.NullIfEmpty()
                                    ?? (p as AudioPost)?.Caption?.NullIfEmpty()
                                    ?? (p as VideoPost)?.Caption?.NullIfEmpty()
                                    ?? p.Url;
                        string artistName = p.RebloggedRootName ?? p.RebloggedFromName ?? p.BlogName;
                        bool repost = artistName != p.BlogName;

                        if (p is PhotoPost && repost && !a.IncludeRepostedPhotos) continue;
                        if (!(p is PhotoPost) && !repost && !a.IncludeNonPhotos) continue;
                        if (!(p is PhotoPost) && repost && !a.IncludeRepostedNonPhotos) continue;

                        if (p is PhotoPost pp)
                        {
                            foreach (var photo in pp.PhotoSet)
                            {
                                _context.Notifications.Add(new Notification
                                {
                                    UserId = a.UserId,
                                    SourceSite = a.SourceSite,
                                    SourceSiteId = p.Id.ToString(),
                                    ArtistName = artistName,
                                    RepostedByArtistName = p.BlogName,
                                    Url = p.Url,
                                    TextPost = false,
                                    Repost = repost,
                                    ThumbnailUrl = photo.OriginalSize.ImageUrl,
                                    Name = photo.Caption?.NullIfEmpty() ?? pp.Caption?.NullIfEmpty() ?? title,
                                    PostDate = p.Timestamp
                                });
                            }
                        }
                        else
                        {
                            _context.Notifications.Add(new Notification
                            {
                                UserId = a.UserId,
                                SourceSite = a.SourceSite,
                                SourceSiteId = p.Id.ToString(),
                                ArtistName = artistName,
                                RepostedByArtistName = p.BlogName,
                                Url = p.Url,
                                TextPost = true,
                                Repost = repost,
                                ThumbnailUrl = null,
                                Name = title,
                                PostDate = p.Timestamp
                            });
                        }
                    }

                    a.LastChecked = DateTimeOffset.UtcNow;
                    if (posts.Any())
                    {
                        a.LastCheckedSourceSiteId = posts.Select(t => t.Id).Max().ToString();
                    }
                }
            }
        }
    }
}