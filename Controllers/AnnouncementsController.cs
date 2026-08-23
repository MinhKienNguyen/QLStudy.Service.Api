using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Domain.Entities;
using QLStudy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnnouncementsController : ControllerBase
    {
        private readonly QLStudyDbContext _context;

        public AnnouncementsController(QLStudyDbContext context)
        {
            _context = context;
        }

        // GET: api/announcements
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Announcement>>> GetAnnouncements()
        {
            return await _context.Announcements
                .Include(a => a.Class)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        // GET: api/announcements/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Announcement>> GetAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null) return NotFound();
            return announcement;
        }

        // POST: api/announcements
        [HttpPost]
        public async Task<ActionResult<Announcement>> CreateAnnouncement([FromBody] Announcement announcement)
        {
            announcement.CreatedAt = DateTime.UtcNow;
            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAnnouncement), new { id = announcement.Id }, announcement);
        }

        // PUT: api/announcements/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAnnouncement(int id, [FromBody] Announcement announcement)
        {
            if (id != announcement.Id) return BadRequest();

            _context.Entry(announcement).State = EntityState.Modified;
            
            // If created date is not set, set it
            if (announcement.CreatedAt == default)
            {
                announcement.CreatedAt = DateTime.UtcNow;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await AnnouncementExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/announcements/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAnnouncement(int id)
        {
            var announcement = await _context.Announcements.FindAsync(id);
            if (announcement == null) return NotFound();

            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> AnnouncementExists(int id)
        {
            return await _context.Announcements.AnyAsync(e => e.Id == id);
        }
    }
}
