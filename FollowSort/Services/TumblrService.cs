using DontPanic.TumblrSharp;
using DontPanic.TumblrSharp.Client;
using DontPanic.TumblrSharp.OAuth;
using FollowSort.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FollowSort.Services
{
    public interface ITumblrService
    {
        Task RefreshAll(ApplicationDbContext context,
            Token token,
            string userId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            Token token,
            string userId,
            Guid artistId,
            bool save = false);
        Task Refresh(ApplicationDbContext context,
            Token token,
            Artist a,
            bool save = false);
    }

    public class TumblrService : ITumblrService
    {
        private readonly string _consumerKey, _consumerSecret;

        public TumblrService(string consumerKey, string consumerSecret)
        {
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
        }

        public async Task RefreshAll(ApplicationDbContext context,
            Token token,
            string userId,
            bool save = false)
        {
            var artists = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.SourceSite == SourceSite.Tumblr)
                .ToListAsync();

            await Task.WhenAll(artists.Select(a => Refresh(context, token, a)));
            if (save) await context.SaveChangesAsync();
        }

        public async Task Refresh(ApplicationDbContext context,
            Token token,
            string userId,
            Guid artistId,
            bool save = false)
        {
            Artist a = await context.Artists
                .Where(x => x.UserId == userId)
                .Where(x => x.Id == artistId)
                .SingleOrDefaultAsync();

            if (a == null)
                throw new Exception($"Artist {artistId} not found");
            if (a.SourceSite != SourceSite.Tumblr)
                throw new Exception($"Artist {artistId} is not a Tumblr user");

            await Refresh(context, token, a, save);
        }

        private static async Task<IList<BasePost>> GetPosts(TumblrClient client, Artist a)
        {
            var posts = new List<BasePost>();
            for (int i = 0; i < (a.LastCheckedSourceSiteId == null ? 1 : 10); i++)
            {
                var x = await client.GetPostsAsync(
                    a.Name,
                    i * 20,
                    20,
                    includeReblogInfo: true,
                    filter: PostFilter.Text);
                foreach (var p in x.Result)
                {
                    if (p.Timestamp <= a.LastChecked)
                    {
                        return posts;
                    }
                    posts.Add(p);
                }
            }
            return posts;
        }

        public async Task Refresh(ApplicationDbContext context,
            Token token,
            Artist a,
            bool save = false)
        {
            if (!token.IsValid)
            {
                throw new Exception("Cannot get new notifications from Tumblr (not logged in or invalid token)");
            }

            using (var client = new TumblrClientFactory().Create<TumblrClient>(_consumerKey, _consumerSecret, token))
            {
                var posts = await GetPosts(client, a);

                foreach (var p in posts)
                {
                    string title = (p as TextPost)?.Title?.NullIfEmpty()
                                ?? (p as TextPost)?.Body?.NullIfEmpty()
                                ?? (p as QuotePost)?.Text?.NullIfEmpty()
                                ?? (p as LinkPost)?.Title?.NullIfEmpty()
                                ?? (p as ChatPost)?.Title?.NullIfEmpty()
                                ?? (p as AudioPost)?.Caption?.NullIfEmpty()
                                ?? (p as VideoPost)?.Caption?.NullIfEmpty()
                                ?? p.Url;
                    string artistName = p.RebloggedRootName ?? p.RebloggedFromName ?? p.BlogName;
                    bool repost = artistName != p.BlogName;

                    if (p is PhotoPost && repost && !a.IncludeRepostedPhotos) continue;
                    if (!(p is PhotoPost) && !repost && !a.IncludeNonPhotos) continue;
                    if (!(p is PhotoPost) && repost && !a.IncludeRepostedNonPhotos) continue;

                    if (p is PhotoPost pp)
                    {
                        foreach (var photo in pp.PhotoSet)
                        {
                            context.Notifications.Add(new Notification
                            {
                                UserId = a.UserId,
                                SourceSite = a.SourceSite,
                                SourceSiteId = p.Id.ToString(),
                                ArtistName = artistName,
                                RepostedByArtistName = p.BlogName,
                                Url = p.Url,
                                TextPost = false,
                                Repost = repost,
                                ThumbnailUrl = photo.OriginalSize.ImageUrl,
                                Name = photo.Caption?.NullIfEmpty() ?? pp.Caption?.NullIfEmpty() ?? title,
                                PostDate = p.Timestamp
                            });
                        }
                    }
                    else
                    {
                        context.Notifications.Add(new Notification
                        {
                            UserId = a.UserId,
                            SourceSite = a.SourceSite,
                            SourceSiteId = p.Id.ToString(),
                            ArtistName = artistName,
                            RepostedByArtistName = p.BlogName,
                            Url = p.Url,
                            TextPost = true,
                            Repost = repost,
                            ThumbnailUrl = null,
                            Name = title,
                            PostDate = p.Timestamp
                        });
                    }
                }

                a.LastChecked = DateTimeOffset.UtcNow;
                if (posts.Any())
                {
                    a.LastCheckedSourceSiteId = posts.Select(t => t.Id).Max().ToString();
                }
            }

            if (save) await context.SaveChangesAsync();
        }
    }
}
