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
    [Route("api/twitter")]
    public class TwitterApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITwitterService _twitterService;
        
        public TwitterApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITwitterService twitterService)
        {
            _context = context;
            _userManager = userManager;
            _twitterService = twitterService;
        }

        [HttpPost, Route("refresh")]
        public async Task RefreshAll()
        {
            var user = await _userManager.GetUserAsync(User);
            var creds = new TwitterCredentials(_twitterService)
            {
                AccessToken = await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token"),
                AccessTokenSecret = await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token_secret")
            };

            await _twitterService.RefreshAll(_context, creds, user.Id, save: true);
        }

        [HttpPost, Route("refresh/{id}")]
        public async Task Refresh(Guid id, bool save = true)
        {
            var user = await _userManager.GetUserAsync(User);
            var creds = new TwitterCredentials(_twitterService)
            {
                AccessToken = await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token"),
                AccessTokenSecret = await _userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token_secret")
            };

            await _twitterService.Refresh(_context, creds, user.Id, id, save: true);
        }
    }
}
