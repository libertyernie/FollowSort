﻿using System;
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
        private readonly IWeasylService _weasylService;
        private readonly IFurAffinityService _furAffinityService;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITumblrService tumblrService,
            ITwitterService twitterService,
            IDeviantArtService deviantArtService,
            IWeasylService weasylService,
            IFurAffinityService furAffinityService)
        {
            _context = context;
            _userManager = userManager;
            _tumblrService = tumblrService;
            _twitterService = twitterService;
            _deviantArtService = deviantArtService;
            _weasylService = weasylService;
            _furAffinityService = furAffinityService;
        }

        public async Task<IActionResult> Index()
        {
            string userId = _userManager.GetUserId(User);
            var q = _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.PostDate)
                .ThenByDescending(n => n.SourceSiteId);
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
            
            await Task.WhenAll(
                _twitterService.RefreshAll(_context, await _userManager.GetTwitterCredentialsAsync(_twitterService, user), user.Id),
                _tumblrService.RefreshAll(_context, await _userManager.GetTumblrTokenAsync(user), user.Id),
                _deviantArtService.RefreshAll(_context, user.Id),
                _weasylService.RefreshAll(_context, user.WeasylApiKey, user.Id),
                _furAffinityService.RefreshAll(_context, user.Id));
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}