using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FollowSort.Data;
using FollowSort.Models;
using FollowSort.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FollowSort.Controllers
{
    [Produces("application/json")]
    [Route("api/artists")]
    public class ArtistsApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ArtistsApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
        public async Task<IActionResult> Refresh(Guid id, bool save = true)
        {
            var artist = await Get(id);
            switch (artist.SourceSite)
            {
                case SourceSite.Tumblr:
                    return Redirect($"/api/tumblr/refresh/{id}");
                case SourceSite.Twitter:
                    return Redirect($"/api/twitter/refresh/{id}");
                case SourceSite.DeviantArt:
                    return Redirect($"/api/deviantart/refresh/{id}");
                default:
                    return NotFound();
            }
        }

        [HttpGet("{id}/avatar")]
        public async Task<IActionResult> GetAvatar(Guid id)
        {
            var artist = await Get(id);
            switch (artist.SourceSite) {
                case SourceSite.Tumblr:
                    return Redirect($"/api/tumblr/avatar/byname/{WebUtility.UrlEncode(artist.Name)}");
                case SourceSite.Twitter:
                    return Redirect($"/api/twitter/avatar/byname/{WebUtility.UrlEncode(artist.Name)}");
                case SourceSite.DeviantArt:
                    return Redirect($"/api/deviantart/avatar/byname/{WebUtility.UrlEncode(artist.Name)}");
                default:
                    return NotFound();
            }
        }
    }
}
