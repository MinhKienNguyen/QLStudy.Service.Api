using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : BaseApiController
    {
        public AttendanceController(QLStudyDbContext context) : base(context)
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
                    return Forbid("Bạn không có quyền truy cập lớp học này.");
                }
            }

            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Định dạng ngày không hợp lệ. Vui lòng sử dụng định dạng yyyy-MM-dd.");
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
            var students = await _context.StudentClasses
                .Include(sc => sc.Student)
                .Where(sc => sc.ClassId == classId)
                .Where(sc => sc.Student != null)
                .OrderBy(sc => sc.Student!.Name)
                .Select(sc => sc.Student!)
                .ToListAsync();

            var defaultRecords = students.Select(s => new AttendanceDto
            {
                StudentId = s!.Id,
                StudentName = s.Name,
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
                    return Forbid("Bạn không có quyền điểm danh lớp học này.");
                }
            }

            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Định dạng ngày không hợp lệ. Vui lòng sử dụng định dạng yyyy-MM-dd.");
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
            return Ok(new { message = "Lưu điểm danh thành công!" });
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
                    return Forbid("Bạn không có quyền xem lịch sử điểm danh lớp học này.");
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
                return BadRequest("Định dạng ngày không hợp lệ. Vui lòng sử dụng định dạng yyyy-MM-dd.");
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
    }
}
