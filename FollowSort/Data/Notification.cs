using FollowSort.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace FollowSort.Data
{
    public class Notification
    {
        public Guid Id { get; set; }

        public string UserId { get; set; }

        public SourceSite SourceSite { get; set; }

        public string SourceSiteId { get; set; }

        [Required]
        public string ArtistName { get; set; }

        [Required]
        public string Url { get; set; }

        public bool TextPost { get; set; }

        public bool Repost { get; set; }

        public string ThumbnailUrl { get; set; }

        public string Name { get; set; }

        public DateTimeOffset PostDate { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser ApplicationUser { get; set; }
    }
}
