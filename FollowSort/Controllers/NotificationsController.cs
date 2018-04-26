using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FollowSort.Data;
using FollowSort.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace FollowSort.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConsumerCredentials _twitterConsumerCredentials;

        public NotificationsController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConsumerCredentials twitterConsumerCredentials)
        {
            _context = context;
            _userManager = userManager;
            _twitterConsumerCredentials = twitterConsumerCredentials;
        }

        public async Task<IActionResult> Index()
        {
            string userId = _userManager.GetUserId(User);
            var q = _context.Notifications
                .Where(n => n.UserId == userId)
                //.Where(n => !n.TextPost)
                //.Where(n => !n.Repost)
                ;
            return View(await q.ToListAsync());
        }

        public async Task<IActionResult> Refresh()
        {
            string userId = _userManager.GetUserId(User);
            var artists = await _context.Artists.Where(a => a.UserId == userId).ToListAsync();
            await Task.WhenAll(RefreshTwitter(artists));
            return NoContent();
        }

        private async Task RefreshTwitter(IEnumerable<Artist> artists)
        {
            var user = await _userManager.GetUserAsync(User);
            await Auth.ExecuteOperationWithCredentials(new TwitterCredentials(
                _twitterConsumerCredentials.ConsumerKey,
                _twitterConsumerCredentials.ConsumerSecret,
                await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token"),
                await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token_secret")
            ), async () =>
            {
                foreach (var a in artists)
                {
                    if (a.SourceSite != SourceSite.Twitter) continue;

                    var tweets = await TimelineAsync.GetUserTimeline(new UserIdentifier(a.Name), new UserTimelineParameters
                    {
                        SinceId = long.TryParse(a.LastCheckedSourceSiteId, out long l) ? l : 20,
                        IncludeEntities = true,
                        IncludeRTS = a.IncludeReposts,
                        MaximumNumberOfTweetsToRetrieve = 10
                    });

                    if (tweets == null)
                    {
                        throw new Exception(ExceptionHandler.GetLastException().TwitterDescription, ExceptionHandler.GetLastException().WebException);
                    }

                    foreach (var t in tweets)
                    {
                        var photos = t.Entities.Medias.Where(m => m.MediaType == "photo");
                        if (photos.Any())
                        {
                            foreach (var p in photos)
                            {
                                _context.Notifications.Add(new Notification
                                {
                                    UserId = a.UserId,
                                    SourceSite = a.SourceSite,
                                    SourceSiteId = t.IdStr,
                                    ArtistName = a.Name,
                                    Url = t.Url,
                                    TextPost = false,
                                    Repost = t.IsRetweet,
                                    ThumbnailUrl = p.MediaURLHttps,
                                    Name = t.Text,
                                    PostDate = t.CreatedAt
                                });
                            }
                        } else
                        {
                            _context.Notifications.Add(new Notification
                            {
                                UserId = a.UserId,
                                SourceSite = a.SourceSite,
                                SourceSiteId = t.IdStr,
                                ArtistName = a.Name,
                                Url = t.Url,
                                TextPost = true,
                                Repost = t.IsRetweet,
                                ThumbnailUrl = null,
                                Name = t.Text,
                                PostDate = t.CreatedAt
                            });
                        }
                    }

                    a.LastChecked = DateTimeOffset.UtcNow;
                    a.LastCheckedSourceSiteId = tweets.Select(t => t.Id).DefaultIfEmpty(20).Max().ToString();
                    await _context.SaveChangesAsync();
                }
            });
        }
    }
}