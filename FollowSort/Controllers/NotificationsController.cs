using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FollowSort.Data;
using FollowSort.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FollowSort.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            string userId = _userManager.GetUserId(User);
            var q = _context.Notifications
                .Where(n => n.UserId == userId)
                .Where(n => !n.TextPost)
                .Where(n => !n.Repost);
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
            foreach (var a in artists)
            {
                if (a.SourceSite != SourceSite.Twitter) continue;

                _context.Notifications.Add(new Notification
                {
                    UserId = a.UserId,
                    SourceSite = a.SourceSite,
                    ArtistName = a.Name,
                    Url = "https://www.example.com",
                    TextPost = false,
                    Repost = false,
                    ThumbnailUrl = "https://orig00.deviantart.net/0942/f/2018/106/6/d/cmdr__sheleth_by_lizard_socks-dc90g7u.png",
                    Name = "dfg thrtu rrgdt thty th thfth ft ft hjfthj ftj ft",
                    PostDate = DateTimeOffset.UtcNow
                });
                a.LastChecked = DateTimeOffset.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}