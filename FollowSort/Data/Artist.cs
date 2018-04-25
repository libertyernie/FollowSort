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

        public bool IncludeTextPosts { get; set; }

        public bool IncludeReposts { get; set; }

        public DateTimeOffset LastChecked { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User;
    }
}
