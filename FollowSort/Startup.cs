using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FollowSort.Data;
using FollowSort.Models;
using FollowSort.Services;
using Tweetinvi.Models;
using Tweetinvi;

namespace FollowSort
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<ApplicationUser, IdentityRole>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
            })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddAuthentication().AddTwitter(o => {
                o.ConsumerKey = Configuration["Authentication:Twitter:ConsumerKey"];
                o.ConsumerSecret = Configuration["Authentication:Twitter:ConsumerSecret"];
                o.SaveTokens = true;
            }).AddDeviantArt(o => {
                o.ClientId = Configuration["Authentication:DeviantArt:ClientId"];
                o.ClientSecret = Configuration["Authentication:DeviantArt:ClientSecret"];
            }).AddTumblr(o => {
                o.ConsumerKey = Configuration["Authentication:Tumblr:ConsumerKey"];
                o.ConsumerSecret = Configuration["Authentication:Tumblr:ConsumerSecret"];
                o.SaveTokens = true;
            });

            // Add application services.
            services.AddTransient<IEmailSender, EmailSender>();

            services.AddMemoryCache();

            services.AddSingleton(typeof(ITwitterService), new TwitterService(
                Configuration["Authentication:Twitter:ConsumerKey"],
                Configuration["Authentication:Twitter:ConsumerSecret"]));
            services.AddSingleton(typeof(ITumblrService), new TumblrService(
                Configuration["Authentication:Tumblr:ConsumerKey"],
                Configuration["Authentication:Tumblr:ConsumerSecret"]));
            services.AddSingleton(typeof(IDeviantArtService), new DeviantArtService());
            services.AddSingleton(typeof(IWeasylService), new WeasylService());
            services.AddSingleton(typeof(IFurAffinityService), new FurAffinityService());

            DeviantartApi.Requester.AppClientId = Configuration["Authentication:DeviantArt:ClientId"];
            DeviantartApi.Requester.AppSecret = Configuration["Authentication:DeviantArt:ClientSecret"];

            TweetinviConfig.CurrentThreadSettings.TweetMode = TweetMode.Extended;
            
            services.AddMvc()
                .AddSessionStateTempDataProvider()
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                    options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                });

            services.AddSession();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseSession();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Notifications}/{action=Index}/{id?}");
            });
        }
    }
}
