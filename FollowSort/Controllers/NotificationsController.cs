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
        private readonly ITumblrService _tumblrService;
        private readonly ITwitterService _twitterService;
        private readonly IDeviantArtService _deviantArtService;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITumblrService tumblrService,
            ITwitterService twitterService,
            IDeviantArtService deviantArtService)
        {
            _context = context;
            _userManager = userManager;
            _tumblrService = tumblrService;
            _twitterService = twitterService;
            _deviantArtService = deviantArtService;
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

        [HttpPost]
        public async Task<IActionResult> Refresh()
        {
            var user = await _userManager.GetUserAsync(User);

            var token = new DontPanic.TumblrSharp.OAuth.Token(
               await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token"),
               await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token_secret"));

            var creds = new TwitterCredentials(_twitterService)
            {
                AccessToken = await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token"),
                AccessTokenSecret = await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token_secret")
            };

            var daToken = await _userManager.GetAuthenticationTokenAsync(user, "DeviantArt", "access_token");

            await Task.WhenAll(
                _twitterService.RefreshAll(_context, creds, user.Id),
                _tumblrService.RefreshAll(_context, token, user.Id),
                _deviantArtService.RefreshAll(_context, daToken, user.Id));
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}