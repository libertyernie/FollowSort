using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace FollowSort.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; }

        [Column(TypeName = "CHAR(48)")]
        public string WeasylApiKey { get; set; }

        [Column(TypeName = "CHAR(40)")]
        public string FurryNetworkBearerToken { get; set; }
    }
}
