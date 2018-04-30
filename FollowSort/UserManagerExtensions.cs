using DontPanic.TumblrSharp.OAuth;
using FollowSort.Data;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tweetinvi.Models;

namespace FollowSort
{
    public static class UserManagerExtensions
    {
        public static async Task<ITwitterCredentials> GetTwitterCredentialsAsync(this UserManager<ApplicationUser> userManager, IConsumerCredentials twitterService, ApplicationUser user)
        {
            return new TwitterCredentials(twitterService)
            {
                AccessToken = await userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token"),
                AccessTokenSecret = await userManager.GetAuthenticationTokenAsync(user, "Twitter", "access_token_secret")
            };
        }

        public static async Task<Token> GetTumblrTokenAsync(this UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            return new Token(
                await userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token"),
                await userManager.GetAuthenticationTokenAsync(user, "Tumblr", "access_token_secret"));
        }

        public static Task<string> GetDeviantArtAccessToken(this UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            return userManager.GetAuthenticationTokenAsync(user, "DeviantArt", "access_token");
        }
    }
}
