using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DontPanic.TumblrSharp.Client;
using FollowSort.Data;
using FollowSort.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FollowSort.Controllers
{
    [Produces("application/json")]
    [Route("api/tumblr")]
    public class TumblrApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITumblrService _tumblrService;

        public TumblrApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITumblrService tumblrService)
        {
            _context = context;
            _userManager = userManager;
            _tumblrService = tumblrService;
        }

        [HttpPost, Route("refresh")]
        public async Task RefreshAll()
        {
            var user = await _userManager.GetUserAsync(User);

            var token = new DontPanic.TumblrSharp.OAuth.Token(
                await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token"),
                await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token_secret"));

            await _tumblrService.RefreshAll(
                _context,
                token,
                user.Id,
                save: true);
        }
        
        [HttpPost, Route("refresh/{id}")]
        public async Task Refresh(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);

            var token = new DontPanic.TumblrSharp.OAuth.Token(
               await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token"),
               await _userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token_secret"));

            await _tumblrService.Refresh(
                _context,
                token,
                user.Id,
                id,
                save: true);
        }

        [HttpGet, Route("avatar/byname/{name}")]
        public IActionResult GetAvatar(string name, int? size = null)
        {
            int newSize = new int[] { 16, 24, 30, 40, 48, 64, 96, 128, 512 }
                .Where(s => s >= size)
                .DefaultIfEmpty(512)
                .First();
            return Redirect($"https://api.tumblr.com/v2/blog/{name}/avatar/{newSize}");
        }
    }
}
