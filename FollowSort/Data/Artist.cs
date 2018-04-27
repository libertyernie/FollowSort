using FollowSort.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace FollowSort.Data
{
    public class Artist
    {
        public Guid Id { get; set; }

        public string UserId { get; set; }

        public SourceSite SourceSite { get; set; }

        [Required]
        public string Name { get; set; }

        [Display(Name = "Include reposted photos"), Column("IncludeReposts")]
        public bool IncludeRepostedPhotos { get; set; }

        [Display(Name = "Include original posts w/o photos"), Column("IncludeTextPosts")]
        public bool IncludeNonPhotos { get; set; }

        [Display(Name = "Include reposted posts w/o photos")]
        public bool IncludeRepostedNonPhotos { get; set; }

        public DateTimeOffset LastChecked { get; set; }

        public string LastCheckedSourceSiteId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User;
    }
}
