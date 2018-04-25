// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Authentication.Tumblr
{
    /// <summary>
    /// Options for the Tumblr authentication handler.
    /// </summary>
    public class TumblrOptions : RemoteAuthenticationOptions
    {
        private const string DefaultStateCookieName = "__TumblrState";

        private CookieBuilder _stateCookieBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="TumblrOptions"/> class.
        /// </summary>
        public TumblrOptions()
        {
            CallbackPath = new PathString("/signin-tumblr");
            BackchannelTimeout = TimeSpan.FromSeconds(60);
            Events = new TumblrEvents();

            ClaimActions.MapJsonKey(ClaimTypes.Name, "name", ClaimValueTypes.String);
            ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "name", ClaimValueTypes.String);

            _stateCookieBuilder = new TumblrCookieBuilder(this)
            {
                Name = DefaultStateCookieName,
                SecurePolicy = CookieSecurePolicy.SameAsRequest,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
            };
        }

        /// <summary>
        /// Gets or sets the consumer key used to communicate with Tumblr.
        /// </summary>
        /// <value>The consumer key used to communicate with Tumblr.</value>
        public string ConsumerKey { get; set; }

        /// <summary>
        /// Gets or sets the consumer secret used to sign requests to Tumblr.
        /// </summary>
        /// <value>The consumer secret used to sign requests to Tumblr.</value>
        public string ConsumerSecret { get; set; }
        
        /// <summary>
        /// A collection of claim actions used to select values from the json user data and create Claims.
        /// </summary>
        public ClaimActionCollection ClaimActions { get; } = new ClaimActionCollection();

        /// <summary>
        /// Gets or sets the type used to secure data handled by the handler.
        /// </summary>
        public ISecureDataFormat<RequestToken> StateDataFormat { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="TumblrEvents"/> used to handle authentication events.
        /// </summary>
        public new TumblrEvents Events
        {
            get => (TumblrEvents)base.Events;
            set => base.Events = value;
        }

        /// <summary>
        /// Determines the settings used to create the state cookie before the
        /// cookie gets added to the response.
        /// </summary>
        public CookieBuilder StateCookie
        {
            get => _stateCookieBuilder;
            set => _stateCookieBuilder = value ?? throw new ArgumentNullException(nameof(value));
        }

        private class TumblrCookieBuilder : CookieBuilder
        {
            private readonly TumblrOptions _twitterOptions;

            public TumblrCookieBuilder(TumblrOptions twitterOptions)
            {
                _twitterOptions = twitterOptions;
            }

            public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
            {
                var options = base.Build(context, expiresFrom);
                if (!Expiration.HasValue)
                {
                    options.Expires = expiresFrom.Add(_twitterOptions.RemoteAuthenticationTimeout);
                }
                return options;
            }
        }
    }
}
