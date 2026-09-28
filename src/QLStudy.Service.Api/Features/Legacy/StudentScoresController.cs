using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Infrastructure.Data;
using QLStudy.Domain.Entities;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentScoresControllerLogic : BaseApiController
    {
        public StudentScoresControllerLogic(QLStudyDbContext context) : base(context)
        {
        }

        public record StudentScoreDto(int StudentId, int ClassId, string Date, string TestName, decimal Score, decimal MaxScore, string? Note);

        [HttpGet]
        public async Task<IActionResult> GetScores(int studentId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            if (!await CanAccessStudent(user, studentId)) return Forbid();

            var scores = await _context.StudentScores
                .Include(s => s.Class)
                .Where(s => s.StudentId == studentId)
                .OrderByDescending(s => s.Date)
                .ThenByDescending(s => s.Id)
                .Select(s => new
                {
                    s.Id,
                    s.StudentId,
                    s.ClassId,
                    className = s.Class!.Name,
                    date = s.Date.ToString("yyyy-MM-dd"),
                    s.TestName,
                    s.Score,
                    s.MaxScore,
                    s.Note
                })
                .ToListAsync();

            return Ok(scores);
        }

        [HttpPost]
        public async Task<IActionResult> CreateScore([FromBody] StudentScoreDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            if (!TryParseDate(dto.Date, out var parsedDate))
            {
                return BadRequest(new { message = "NgÃ y kiá»ƒm tra khÃ´ng há»£p lá»‡." });
            }

            var testName = dto.TestName.Trim();
            if (string.IsNullOrWhiteSpace(testName))
            {
                return BadRequest(new { message = "Vui lÃ²ng nháº­p tÃªn bÃ i kiá»ƒm tra." });
            }

            if (dto.MaxScore <= 0 || dto.Score < 0 || dto.Score > dto.MaxScore)
            {
                return BadRequest(new { message = "Äiá»ƒm kiá»ƒm tra khÃ´ng há»£p lá»‡." });
            }

            var studentInClass = await _context.StudentClasses
                .AnyAsync(sc => sc.StudentId == dto.StudentId && sc.ClassId == dto.ClassId);

            if (!studentInClass)
            {
                return BadRequest(new { message = "Há»c sinh khÃ´ng thuá»™c lá»›p Ä‘Ã£ chá»n." });
            }

            var score = new StudentScore
            {
                StudentId = dto.StudentId,
                ClassId = dto.ClassId,
                Date = parsedDate,
                TestName = testName,
                Score = dto.Score,
                MaxScore = dto.MaxScore,
                Note = dto.Note?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.StudentScores.Add(score);
            await _context.SaveChangesAsync();
            return Ok(score);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteScore(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var score = await _context.StudentScores.FindAsync(id);
            if (score == null) return NotFound();

            _context.StudentScores.Remove(score);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<bool> CanAccessStudent(User user, int studentId)
        {
            if (user.Role == "Manager") return true;

            var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
            return await _context.StudentClasses
                .AnyAsync(sc => sc.StudentId == studentId && sc.Class != null && sc.Class.SubjectId != null && subjectIds.Contains(sc.Class.SubjectId.Value));
        }

        private static bool TryParseDate(string date, out DateTime parsedDate)
        {
            parsedDate = default;
            if (!DateTime.TryParse(date, out var value)) return false;
            parsedDate = DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
            return true;
        }
    }
}

