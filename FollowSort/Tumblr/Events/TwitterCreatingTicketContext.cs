// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Authentication.Tumblr
{
    /// <summary>
    /// Contains information about the login session as well as the user <see cref="System.Security.Claims.ClaimsIdentity"/>.
    /// </summary>
    public class TumblrCreatingTicketContext : ResultContext<TumblrOptions>
    {
        /// <summary>
        /// Initializes a <see cref="TumblrCreatingTicketContext"/>
        /// </summary>
        /// <param name="context">The HTTP environment</param>
        /// <param name="scheme">The scheme data</param>
        /// <param name="options">The options for Tumblr</param>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/>.</param>
        /// <param name="properties">The <see cref="AuthenticationProperties"/>.</param>
        /// <param name="userId">Tumblr user ID</param>
        /// <param name="screenName">Tumblr screen name</param>
        /// <param name="accessToken">Tumblr access token</param>
        /// <param name="accessTokenSecret">Tumblr access token secret</param>
        /// <param name="user">User details</param>
        public TumblrCreatingTicketContext(
            HttpContext context,
            AuthenticationScheme scheme,
            TumblrOptions options,
            ClaimsPrincipal principal,
            AuthenticationProperties properties,
            string accessToken,
            string accessTokenSecret,
            JObject user)
            : base(context, scheme, options)
        {
            AccessToken = accessToken;
            AccessTokenSecret = accessTokenSecret;
            User = user ?? new JObject();
            Principal = principal;
            Properties = properties;
        }
        
        /// <summary>
        /// Gets the Tumblr access token
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Gets the Tumblr access token secret
        /// </summary>
        public string AccessTokenSecret { get; }

        /// <summary>
        /// Gets the JSON-serialized user or an empty
        /// <see cref="JObject"/> if it is not available.
        /// </summary>
        public JObject User { get; }
    }
}
