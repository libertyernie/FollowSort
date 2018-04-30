using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FollowSort.Models.ManageViewModels
{
    public class AdditionalSourcesViewModel
    {
        [Display(Name = "Weasyl API key")]
        public string WeasylApiKey { get; set; }

        [Display(Name = "Furry Network bearer token")]
        public string FurryNetworkBearerToken { get; set; }

        public string StatusMessage { get; set; }
    }
}
