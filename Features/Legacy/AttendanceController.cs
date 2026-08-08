using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Infrastructure.Data;
using QLStudy.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceControllerLogic : BaseApiController
    {
        public AttendanceControllerLogic(QLStudyDbContext context) : base(context)
        {
        }

        public class AttendanceDto
        {
            public int StudentId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string Status { get; set; } = "Present";
            public string Note { get; set; } = string.Empty;
        }

        public class SaveAttendanceDto
        {
            public int StudentId { get; set; }
            public string Status { get; set; } = "Present";
            public string Note { get; set; } = string.Empty;
        }

        // GET: api/attendance?classId=5&date=2026-06-23
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AttendanceDto>>> GetAttendance(int classId, string date)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            // Verify teacher subject scope
            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                var targetClass = await _context.Classes.FindAsync(classId);
                if (targetClass == null || targetClass.SubjectId == null || !subjectIds.Contains(targetClass.SubjectId.Value))
                {
                    return Forbid("Báº¡n khÃ´ng cÃ³ quyá»n truy cáº­p lá»›p há»c nÃ y.");
                }
            }

            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Äá»‹nh dáº¡ng ngÃ y khÃ´ng há»£p lá»‡. Vui lÃ²ng sá»­ dá»¥ng Ä‘á»‹nh dáº¡ng yyyy-MM-dd.");
            }

            parsedDate = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            var nextDate = parsedDate.AddDays(1);

            // Fetch existing attendance records
            var records = await _context.Attendances
                .Include(a => a.Student)
                .Where(a => a.ClassId == classId && a.Date >= parsedDate && a.Date < nextDate)
                .ToListAsync();

            if (records.Count > 0)
            {
                var result = records.Select(r => new AttendanceDto
                {
                    StudentId = r.StudentId,
                    StudentName = r.Student?.Name ?? "N/A",
                    Status = r.Status,
                    Note = r.Note
                }).ToList();

                return Ok(result);
            }

            // If no records exist, fetch all active students in the class
            var students = await _context.StudentClassEnrollments
                .Include(e => e.Student)
                .Include(e => e.Class)
                .Where(e => e.ClassId == classId && e.Status == "Active")
                .Where(e => e.Student != null)
                .ToListAsync();

            students = students
                .Where(e => IsEnrollmentActiveForDate(e, parsedDate, e.Class!.StartDate, e.Class!.EndDate))
                .OrderBy(e => e.Student!.Name)
                .ToList();

            var defaultRecords = students.Select(e => new AttendanceDto
            {
                StudentId = e.Student!.Id,
                StudentName = e.Student.Name,
                Status = "Present",
                Note = string.Empty
            }).OrderBy(s => s.StudentName).ToList();

            return Ok(defaultRecords);
        }

        // POST: api/attendance?classId=5&date=2026-06-23
        [HttpPost]
        public async Task<IActionResult> SaveAttendance(int classId, string date, [FromBody] List<SaveAttendanceDto> dtos)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            // Verify teacher subject scope
            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                var targetClass = await _context.Classes.FindAsync(classId);
                if (targetClass == null || targetClass.SubjectId == null || !subjectIds.Contains(targetClass.SubjectId.Value))
                {
                    return Forbid("Báº¡n khÃ´ng cÃ³ quyá»n Ä‘iá»ƒm danh lá»›p há»c nÃ y.");
                }
            }

            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Äá»‹nh dáº¡ng ngÃ y khÃ´ng há»£p lá»‡. Vui lÃ²ng sá»­ dá»¥ng Ä‘á»‹nh dáº¡ng yyyy-MM-dd.");
            }

            parsedDate = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            var nextDate = parsedDate.AddDays(1);

            // Load existing records
            var existingRecords = await _context.Attendances
                .Where(a => a.ClassId == classId && a.Date >= parsedDate && a.Date < nextDate)
                .ToListAsync();

            foreach (var dto in dtos)
            {
                var record = existingRecords.FirstOrDefault(r => r.StudentId == dto.StudentId);
                if (record != null)
                {
                    record.Status = dto.Status;
                    record.Note = dto.Note;
                }
                else
                {
                    _context.Attendances.Add(new Attendance
                    {
                        ClassId = classId,
                        StudentId = dto.StudentId,
                        Date = parsedDate,
                        Status = dto.Status,
                        Note = dto.Note
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "LÆ°u Ä‘iá»ƒm danh thÃ nh cÃ´ng!" });
        }

        // GET: api/attendance/history?classId=5
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(int classId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            // Verify teacher subject scope
            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                var targetClass = await _context.Classes.FindAsync(classId);
                if (targetClass == null || targetClass.SubjectId == null || !subjectIds.Contains(targetClass.SubjectId.Value))
                {
                    return Forbid("Báº¡n khÃ´ng cÃ³ quyá»n xem lá»‹ch sá»­ Ä‘iá»ƒm danh lá»›p há»c nÃ y.");
                }
            }

            var attendances = await _context.Attendances
                .Where(a => a.ClassId == classId)
                .Select(a => new { a.Date, a.Status })
                .ToListAsync();

            var result = attendances
                .GroupBy(a => a.Date.Date)
                .Select(g => {
                    string status = "Normal";
                    if (g.All(x => x.Status == "Holiday"))
                    {
                        status = "Holiday";
                    }
                    else if (g.All(x => x.Status == "ClassOff"))
                    {
                        status = "ClassOff";
                    }
                    return new
                    {
                        date = g.Key.ToString("yyyy-MM-dd"),
                        status = status
                    };
                })
                .OrderByDescending(x => x.date)
                .ToList();

            return Ok(result);
        }

        // DELETE: api/attendance?classId=5&date=2026-06-23
        [HttpDelete]
        public async Task<IActionResult> DeleteAttendance(int classId, string date)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Äá»‹nh dáº¡ng ngÃ y khÃ´ng há»£p lá»‡. Vui lÃ²ng sá»­ dá»¥ng Ä‘á»‹nh dáº¡ng yyyy-MM-dd.");
            }

            parsedDate = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            var nextDate = parsedDate.AddDays(1);

            var records = await _context.Attendances
                .Where(a => a.ClassId == classId && a.Date >= parsedDate && a.Date < nextDate)
                .ToListAsync();

            if (records.Count == 0) return NotFound();

            _context.Attendances.RemoveRange(records);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        private static bool IsEnrollmentActiveForDate(StudentClassEnrollment enrollment, DateTime date, DateOnly? classStartDate, DateOnly? classEndDate)
        {
            var target = new DateOnly(date.Year, date.Month, 1);
            var start = ResolveMonthForClassRange(enrollment.StartMonth, classStartDate, classEndDate);
            if (start == null || target < start.Value) return false;

            if (!string.IsNullOrWhiteSpace(enrollment.EndMonth))
            {
                var end = ResolveMonthForClassRange(enrollment.EndMonth!, classStartDate, classEndDate);
                if (end != null && target > end.Value) return false;
            }

            if (classEndDate != null && target > new DateOnly(classEndDate.Value.Year, classEndDate.Value.Month, 1)) return false;
            return true;
        }

        private static DateOnly? ResolveMonthForClassRange(string monthValue, DateOnly? classStartDate, DateOnly? classEndDate)
        {
            if (TryParsePeriodStart(monthValue, out var explicitMonth)) return explicitMonth;
            if (!TryParseMonth(monthValue, out var month)) return null;

            var baseStart = classStartDate == null
                ? new DateOnly(DateTime.UtcNow.Year, month, 1)
                : new DateOnly(classStartDate.Value.Year, classStartDate.Value.Month, 1);
            var candidate = new DateOnly(baseStart.Year, month, 1);
            if (candidate < baseStart) candidate = candidate.AddYears(1);
            if (classEndDate != null)
            {
                var classEnd = new DateOnly(classEndDate.Value.Year, classEndDate.Value.Month, 1);
                if (candidate > classEnd && candidate.AddYears(-1) >= baseStart)
                {
                    candidate = candidate.AddYears(-1);
                }
            }
            return candidate;
        }

        private static bool TryParsePeriodStart(string value, out DateOnly periodStart)
        {
            periodStart = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim().ToUpperInvariant();
            if (!normalized.StartsWith("T")) return false;
            var parts = normalized[1..].Split('/', '-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            return int.TryParse(parts[0], out var month) && month >= 1 && month <= 12
                && int.TryParse(parts[1], out var year) && year > 0
                && (periodStart = new DateOnly(year, month, 1)) != default;
        }

        private static bool TryParseMonth(string value, out int month)
        {
            month = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim().ToUpperInvariant();
            if (normalized.StartsWith("T")) normalized = normalized[1..];
            normalized = normalized.Split('/', '-', StringSplitOptions.RemoveEmptyEntries)[0];
            return int.TryParse(normalized, out month) && month >= 1 && month <= 12;
        }

    }
}

