using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RewardOptionsController : BaseApiController
    {
        public RewardOptionsController(QLStudyDbContext context) : base(context)
        {
        }

        // GET: api/rewardoptions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RewardOption>>> GetRewardOptions()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            return await _context.RewardOptions
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        // POST: api/rewardoptions
        [HttpPost]
        public async Task<ActionResult<RewardOption>> CreateRewardOption([FromBody] RewardOption option)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            if (string.IsNullOrWhiteSpace(option.Name))
            {
                return BadRequest("Reward name cannot be empty.");
            }

            // Check duplicate
            var exists = await _context.RewardOptions.AnyAsync(r => r.Name.ToLower() == option.Name.ToLower());
            if (exists)
            {
                return BadRequest("This reward category already exists.");
            }

            _context.RewardOptions.Add(option);
            await _context.SaveChangesAsync();

            return Ok(option);
        }

        // DELETE: api/rewardoptions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRewardOption(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var option = await _context.RewardOptions.FindAsync(id);
            if (option == null) return NotFound();

            _context.RewardOptions.Remove(option);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
