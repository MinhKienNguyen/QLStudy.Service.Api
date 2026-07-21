using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectsController : BaseApiController
    {
        public SubjectsController(QLStudyDbContext context) : base(context)
        {
        }

        // GET: api/subjects
        [HttpGet]
        public async Task<IActionResult> GetSubjects()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var subjects = await _context.Subjects.OrderBy(s => s.Name).ToListAsync();
            return Ok(subjects);
        }

        // GET: api/subjects/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSubject(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return NotFound();

            return Ok(subject);
        }

        // POST: api/subjects
        [HttpPost]
        public async Task<IActionResult> CreateSubject([FromBody] Subject subject)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSubject), new { id = subject.Id }, subject);
        }

        // PUT: api/subjects/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSubject(int id, [FromBody] Subject subject)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            if (id != subject.Id) return BadRequest();

            _context.Entry(subject).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await SubjectExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/subjects/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return NotFound();

            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> SubjectExists(int id)
        {
            return await _context.Subjects.AnyAsync(e => e.Id == id);
        }
    }
}
