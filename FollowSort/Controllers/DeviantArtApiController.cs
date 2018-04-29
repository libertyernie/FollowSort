using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FollowSort.Data;
using FollowSort.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tweetinvi;
using Tweetinvi.Models;
using Tweetinvi.Parameters;

namespace FollowSort.Controllers
{
    [Produces("application/json")]
    [Route("api/deviantart")]
    public class DeviantArtApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDeviantArtService _deviantArtService;
        
        public DeviantArtApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IDeviantArtService deviantArtService)
        {
            _context = context;
            _userManager = userManager;
            _deviantArtService = deviantArtService;
        }

        [HttpPost, Route("refresh")]
        public async Task RefreshAll()
        {
            var user = await _userManager.GetUserAsync(User);
            var token = await _userManager.GetAuthenticationTokenAsync(user, "DeviantArt", "access_token");

            await _deviantArtService.RefreshAll(_context, token, user.Id, save: true);
        }

        [HttpPost, Route("refresh/{id}")]
        public async Task Refresh(Guid id, bool save = true)
        {
            var user = await _userManager.GetUserAsync(User);
            var token = await _userManager.GetAuthenticationTokenAsync(user, "DeviantArt", "access_token");

            await _deviantArtService.Refresh(_context, token, user.Id, id, save: true);
        }

        [HttpGet, Route("avatar/byname/{name}")]
        public async Task<IActionResult> GetAvatar(string name, int? size = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var token = await _userManager.GetAuthenticationTokenAsync(user, "DeviantArt", "access_token");

            return Redirect(await _deviantArtService.GetAvatarUrlAsync(token, name));
        }
    }
}
