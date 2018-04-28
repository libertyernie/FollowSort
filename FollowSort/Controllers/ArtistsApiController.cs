using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FollowSort.Data;
using FollowSort.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FollowSort.Controllers
{
    [Produces("application/json")]
    [Route("api/artists")]
    public class ArtistsApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ArtistsApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IEnumerable<Artist>> Get()
        {
            return await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .ToListAsync();
        }
        
        [HttpGet("{id}", Name = "Get")]
        public async Task<Artist> GetAsync(Guid id)
        {
            return await _context.Artists
                .Where(a => a.Id == id)
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync();
        }
    }
}
