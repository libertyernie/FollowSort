using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DontPanic.TumblrSharp.OAuth;
using FollowSort.Data;
using FollowSort.Models;
using FollowSort.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tweetinvi.Models;

namespace FollowSort.Controllers
{
    [Produces("application/json")]
    [Route("api/artists")]
    public class ArtistsApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITwitterService _twitterService;
        private readonly ITumblrService _tumblrService;
        private readonly IDeviantArtService _deviantArtService;
        private readonly IWeasylService _weasylService;

        public ArtistsApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITwitterService twitterService,
            ITumblrService tumblrService,
            IDeviantArtService deviantArtService,
            IWeasylService weasylService)
        {
            _context = context;
            _userManager = userManager;
            _twitterService = twitterService;
            _tumblrService = tumblrService;
            _deviantArtService = deviantArtService;
            _weasylService = weasylService;
        }

        [HttpGet]
        public async Task<IEnumerable<Artist>> Get()
        {
            return await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .ToListAsync();
        }
        
        [HttpGet("{id}")]
        public async Task<Artist> Get(Guid id)
        {
            return await _context.Artists
                .Where(a => a.Id == id)
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync();
        }
        
        [HttpPost, Route("{id}/refresh")]
        public async Task<IActionResult> Refresh(Guid id)
        {
            var user = await _userManager.GetUserAsync(User);
            var artist = await Get(id);
            switch (artist.SourceSite)
            {
                case SourceSite.Tumblr:
                    await _tumblrService.Refresh(_context, await _userManager.GetTumblrTokenAsync(user), user.Id, id, save: true);
                    return NoContent();
                case SourceSite.Twitter:
                    await _twitterService.Refresh(_context, await _userManager.GetTwitterCredentialsAsync(_twitterService, user), user.Id, id, save: true);
                    return NoContent();
                case SourceSite.DeviantArt:
                    await _deviantArtService.Refresh(_context, user.Id, id, save: true);
                    return NoContent();
                case SourceSite.Weasyl:
                    await _weasylService.Refresh(_context, user.WeasylApiKey, user.Id, id, save: true);
                    return NoContent();
                default:
                    return NotFound();
            }
        }

        [HttpGet("{id}/avatar")]
        public async Task<IActionResult> GetAvatar(Guid id, int? size = null)
        {
            var user = await _userManager.GetUserAsync(User);
            var artist = await Get(id);
            string urlName = WebUtility.UrlEncode(artist.Name);
            switch (artist.SourceSite) {
                case SourceSite.Tumblr:
                    int newSize = new int[] { 16, 24, 30, 40, 48, 64, 96, 128, 512 }
                        .Where(s => s >= size)
                        .DefaultIfEmpty(512)
                        .First();
                    return Redirect($"https://api.tumblr.com/v2/blog/{urlName}/avatar/{newSize}");
                case SourceSite.Twitter:
                    return Redirect(await _twitterService.GetAvatarUrlAsync(await _userManager.GetTwitterCredentialsAsync(_twitterService, user), artist.Name));
                case SourceSite.DeviantArt:
                    return Redirect($"https://a.deviantart.net/avatars/{urlName[0]}/{urlName[1]}/{urlName}.png");
                case SourceSite.Weasyl:
                    return Redirect(await _weasylService.GetAvatarUrlAsync(user.WeasylApiKey, artist.Name));
                default:
                    return NotFound();
            }
        }
    }
}
