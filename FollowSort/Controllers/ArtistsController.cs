﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FollowSort.Data;
using Microsoft.AspNetCore.Identity;
using FollowSort.Models;

namespace FollowSort.Controllers
{
    public class ArtistsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ArtistsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Artists
        public async Task<IActionResult> Index()
        {
            return View(await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .ToListAsync());
        }

        // GET: Artists/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var artist = await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync(m => m.Id == id);
            if (artist == null)
            {
                return NotFound();
            }

            return View(artist);
        }

        // GET: Artists/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Artists/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SourceSite,Name,IncludeTextPosts,IncludeReposts")] Artist artist)
        {
            if (ModelState.IsValid)
            {
                artist.Id = Guid.NewGuid();
                artist.UserId = _userManager.GetUserId(User);
                artist.LastChecked = DateTimeOffset.MinValue;
                _context.Add(artist);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(artist);
        }

        // GET: Artists/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var artist = await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync(m => m.Id == id);
            if (artist == null)
            {
                return NotFound();
            }
            return View(artist);
        }

        // POST: Artists/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,IncludeTextPosts,IncludeReposts")] Artist artist)
        {
            if (id != artist.Id)
            {
                return NotFound();
            }

            var existing = await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync(m => m.Id == id);
            if (existing == null)
            {
                return NotFound();
            }
            
            existing.IncludeTextPosts = artist.IncludeTextPosts;
            existing.IncludeReposts = artist.IncludeReposts;
            await _context.SaveChangesAsync();

            return View(artist);
        }

        // GET: Artists/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var artist = await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync(m => m.Id == id);
            if (artist == null)
            {
                return NotFound();
            }

            return View(artist);
        }

        // POST: Artists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var artist = await _context.Artists
                .Where(a => a.UserId == _userManager.GetUserId(User))
                .SingleOrDefaultAsync(m => m.Id == id);
            _context.Artists.Remove(artist);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
