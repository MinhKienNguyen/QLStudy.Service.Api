using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Infrastructure.Data;
using QLStudy.Domain.Entities;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class PenaltiesControllerLogic : BaseApiController
    {
        public PenaltiesControllerLogic(QLStudyDbContext context) : base(context)
        {
        }

        public record PenaltyRuleDto(string Name, decimal DefaultAmount, bool IsActive);
        public record StudentPenaltyDto(int StudentId, int ClassId, int PenaltyRuleId, string Date, decimal Amount, string? Note);

        [HttpGet("rules")]
        public async Task<IActionResult> GetRules()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var rules = await _context.PenaltyRules
                .OrderByDescending(r => r.IsActive)
                .ThenBy(r => r.Name)
                .ToListAsync();

            return Ok(rules);
        }

        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] PenaltyRuleDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Vui lÃ²ng nháº­p tÃªn lá»—i pháº¡t." });
            if (dto.DefaultAmount < 0) return BadRequest(new { message = "Sá»‘ tiá»n pháº¡t khÃ´ng há»£p lá»‡." });

            var rule = new PenaltyRule
            {
                Name = name,
                DefaultAmount = dto.DefaultAmount,
                IsActive = dto.IsActive
            };

            _context.PenaltyRules.Add(rule);
            await _context.SaveChangesAsync();
            return Ok(rule);
        }

        [HttpPut("rules/{id}")]
        public async Task<IActionResult> UpdateRule(int id, [FromBody] PenaltyRuleDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var rule = await _context.PenaltyRules.FindAsync(id);
            if (rule == null) return NotFound();

            var name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Vui lÃ²ng nháº­p tÃªn lá»—i pháº¡t." });
            if (dto.DefaultAmount < 0) return BadRequest(new { message = "Sá»‘ tiá»n pháº¡t khÃ´ng há»£p lá»‡." });

            rule.Name = name;
            rule.DefaultAmount = dto.DefaultAmount;
            rule.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();
            return Ok(rule);
        }

        [HttpDelete("rules/{id}")]
        public async Task<IActionResult> DeleteRule(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var rule = await _context.PenaltyRules.FindAsync(id);
            if (rule == null) return NotFound();

            var inUse = await _context.StudentPenalties.AnyAsync(p => p.PenaltyRuleId == id);
            if (inUse) return BadRequest(new { message = "Lá»—i pháº¡t nÃ y Ä‘Ã£ Ä‘Æ°á»£c sá»­ dá»¥ng, chá»‰ cÃ³ thá»ƒ khÃ³a thay vÃ¬ xÃ³a." });

            _context.PenaltyRules.Remove(rule);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetPenalties(int? classId, string? date)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var query = _context.StudentPenalties
                .Include(p => p.Student)
                .Include(p => p.Class)
                .Include(p => p.PenaltyRule)
                .AsQueryable();

            if (classId != null)
            {
                if (!await CanAccessClass(user, classId.Value)) return Forbid();
                query = query.Where(p => p.ClassId == classId.Value);
            }
            else if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                query = query.Where(p => p.Class != null && p.Class.SubjectId != null && subjectIds.Contains(p.Class.SubjectId.Value));
            }

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!TryParseDate(date, out var parsedDate)) return BadRequest(new { message = "NgÃ y pháº¡t khÃ´ng há»£p lá»‡." });
                var nextDate = parsedDate.AddDays(1);
                query = query.Where(p => p.Date >= parsedDate && p.Date < nextDate);
            }

            var result = await query
                .OrderByDescending(p => p.Date)
                .ThenBy(p => p.Student!.Name)
                .Select(p => new
                {
                    p.Id,
                    p.StudentId,
                    studentName = p.Student!.Name,
                    p.ClassId,
                    className = p.Class!.Name,
                    p.PenaltyRuleId,
                    ruleName = p.PenaltyRule!.Name,
                    date = p.Date.ToString("yyyy-MM-dd"),
                    p.Amount,
                    p.Note
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePenalty([FromBody] StudentPenaltyDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (!await CanAccessClass(user, dto.ClassId)) return Forbid();

            if (!TryParseDate(dto.Date, out var parsedDate)) return BadRequest(new { message = "NgÃ y pháº¡t khÃ´ng há»£p lá»‡." });
            if (dto.Amount < 0) return BadRequest(new { message = "Sá»‘ tiá»n pháº¡t khÃ´ng há»£p lá»‡." });

            var studentInClass = await _context.StudentClasses.AnyAsync(sc => sc.StudentId == dto.StudentId && sc.ClassId == dto.ClassId);
            if (!studentInClass) return BadRequest(new { message = "Há»c sinh khÃ´ng thuá»™c lá»›p Ä‘Ã£ chá»n." });

            var ruleExists = await _context.PenaltyRules.AnyAsync(r => r.Id == dto.PenaltyRuleId && r.IsActive);
            if (!ruleExists) return BadRequest(new { message = "Lá»—i pháº¡t khÃ´ng tá»“n táº¡i hoáº·c Ä‘ang bá»‹ khÃ³a." });

            var penalty = new StudentPenalty
            {
                StudentId = dto.StudentId,
                ClassId = dto.ClassId,
                PenaltyRuleId = dto.PenaltyRuleId,
                Date = parsedDate,
                Amount = dto.Amount,
                Note = dto.Note?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.StudentPenalties.Add(penalty);
            await _context.SaveChangesAsync();
            return Ok(penalty);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePenalty(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var penalty = await _context.StudentPenalties.FindAsync(id);
            if (penalty == null) return NotFound();
            if (!await CanAccessClass(user, penalty.ClassId)) return Forbid();

            _context.StudentPenalties.Remove(penalty);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(int semesterId, string mode = "week", string? from = null, string? to = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var query = _context.StudentPenalties
                .Include(p => p.Student)
                .Include(p => p.Class)
                .Include(p => p.PenaltyRule)
                .Where(p => p.Class != null && p.Class.SemesterId == semesterId);

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                query = query.Where(p => p.Class != null && p.Class.SubjectId != null && subjectIds.Contains(p.Class.SubjectId.Value));
            }

            if (!string.IsNullOrWhiteSpace(from))
            {
                if (!TryParseDate(from, out var fromDate)) return BadRequest(new { message = "Tá»« ngÃ y khÃ´ng há»£p lá»‡." });
                query = query.Where(p => p.Date >= fromDate);
            }

            if (!string.IsNullOrWhiteSpace(to))
            {
                if (!TryParseDate(to, out var toDate)) return BadRequest(new { message = "Äáº¿n ngÃ y khÃ´ng há»£p lá»‡." });
                var nextDate = toDate.AddDays(1);
                query = query.Where(p => p.Date < nextDate);
            }

            var items = await query
                .OrderByDescending(p => p.Date)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    date = p.Date,
                    studentName = p.Student!.Name,
                    className = p.Class!.Name,
                    ruleName = p.PenaltyRule!.Name,
                    p.Note
                })
                .ToListAsync();

            var grouped = items
                .GroupBy(p => mode.Equals("month", StringComparison.OrdinalIgnoreCase)
                    ? $"{p.date:yyyy-MM}"
                    : $"{p.date.AddDays(-(((int)p.date.DayOfWeek + 6) % 7)):yyyy-MM-dd}")
                .Select(g => new
                {
                    period = mode.Equals("month", StringComparison.OrdinalIgnoreCase) ? $"ThÃ¡ng {g.Key}" : $"Tuáº§n tá»« {g.Key}",
                    totalAmount = g.Sum(x => x.Amount),
                    totalCount = g.Count(),
                    students = g.GroupBy(x => x.studentName)
                        .Select(sg => new
                        {
                            studentName = sg.Key,
                            totalAmount = sg.Sum(x => x.Amount),
                            totalCount = sg.Count()
                        })
                        .OrderByDescending(x => x.totalAmount)
                        .ToList(),
                    items = g.Select(x => new
                    {
                        x.Id,
                        date = x.date.ToString("yyyy-MM-dd"),
                        x.studentName,
                        x.className,
                        x.ruleName,
                        x.Amount,
                        x.Note
                    }).ToList()
                })
                .OrderByDescending(g => g.period)
                .ToList();

            return Ok(grouped);
        }

        private async Task<bool> CanAccessClass(User user, int classId)
        {
            if (user.Role == "Manager") return true;

            var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
            return await _context.Classes.AnyAsync(c => c.Id == classId && c.SubjectId != null && subjectIds.Contains(c.SubjectId.Value));
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

