using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClassesController : BaseApiController
    {
        public ClassesController(QLStudyDbContext context) : base(context)
        {
        }

        // GET: api/classes?semesterId=5
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Class>>> GetClasses(int? semesterId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var query = _context.Classes
                .Include(c => c.Schedules)
                .AsQueryable();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                query = query.Where(c => c.SubjectId != null && subjectIds.Contains(c.SubjectId.Value));
            }

            if (semesterId.HasValue)
            {
                query = query.Where(c => c.SemesterId == semesterId.Value);
            }

            return await query.OrderBy(c => c.Name).ToListAsync();
        }

        // GET: api/classes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Class>> GetClass(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var cls = await _context.Classes
                .Include(c => c.Schedules)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cls == null) return NotFound();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                if (cls.SubjectId == null || !subjectIds.Contains(cls.SubjectId.Value))
                {
                    return Forbid();
                }
            }

            return cls;
        }

        // POST: api/classes
        [HttpPost]
        public async Task<ActionResult<Class>> CreateClass(Class cls)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var validationError = await ValidateTeacherAndSubjectAsync(cls.TeacherId, cls.SubjectId);
            if (validationError != null) return BadRequest(new { message = validationError });
            var dateValidationError = ValidateClassDateRange(cls.StartDate, cls.EndDate);
            if (dateValidationError != null) return BadRequest(new { message = dateValidationError });

            _context.Classes.Add(cls);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClass), new { id = cls.Id }, cls);
        }

        // PUT: api/classes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClass(int id, Class cls)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            if (id != cls.Id) return BadRequest();

            var validationError = await ValidateTeacherAndSubjectAsync(cls.TeacherId, cls.SubjectId);
            if (validationError != null) return BadRequest(new { message = validationError });
            var dateValidationError = ValidateClassDateRange(cls.StartDate, cls.EndDate);
            if (dateValidationError != null) return BadRequest(new { message = dateValidationError });

            _context.Entry(cls).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ClassExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/classes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClass(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var cls = await _context.Classes.FindAsync(id);
            if (cls == null) return NotFound();

            var hasStudents = await _context.StudentClasses.AnyAsync(sc => sc.ClassId == id);
            if (hasStudents)
            {
                return BadRequest(new { message = "Lớp học đã có học sinh nên không thể xóa." });
            }

            _context.Classes.Remove(cls);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/classes/5/schedules
        [HttpPost("{classId}/schedules")]
        public async Task<IActionResult> SetSchedules(int classId, [FromBody] List<ClassSchedule> newSchedules)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var cls = await _context.Classes.Include(c => c.Schedules).FirstOrDefaultAsync(c => c.Id == classId);
            if (cls == null) return NotFound();

            var scheduleValidationError = await ValidateScheduleConflictsAsync(cls, newSchedules);
            if (scheduleValidationError != null) return BadRequest(new { message = scheduleValidationError });

            // Clear old schedules
            _context.ClassSchedules.RemoveRange(cls.Schedules);

            // Add new schedules
            foreach (var sched in newSchedules)
            {
                sched.ClassId = classId;
                sched.Id = 0; // Ensure it's treated as new
                _context.ClassSchedules.Add(sched);
            }

            await _context.SaveChangesAsync();
            return Ok(await _context.ClassSchedules.Where(cs => cs.ClassId == classId).ToListAsync());
        }

        private async Task<bool> ClassExists(int id)
        {
            return await _context.Classes.AnyAsync(e => e.Id == id);
        }

        private async Task<string?> ValidateTeacherAndSubjectAsync(int? teacherId, int? subjectId)
        {
            if (teacherId == null) return "Vui lòng chọn giáo viên phụ trách lớp.";
            if (subjectId == null) return "Vui lòng chọn môn học cho lớp.";

            var teacher = await _context.Users
                .Include(u => u.UserSubjects)
                .FirstOrDefaultAsync(u => u.Id == teacherId.Value && u.Role == "Teacher" && u.Status == "Active");

            if (teacher == null)
            {
                return "Giáo viên không tồn tại hoặc đang bị khóa.";
            }

            if (!teacher.UserSubjects.Any(us => us.SubjectId == subjectId.Value))
            {
                return "Môn học đã chọn không thuộc phạm vi môn của giáo viên này.";
            }

            return null;
        }

        private static string? ValidateClassDateRange(DateOnly? startDate, DateOnly? endDate)
        {
            if (startDate != null && endDate != null && endDate < startDate)
            {
                return "Ngày kết thúc lớp học phải lớn hơn hoặc bằng ngày bắt đầu.";
            }

            return null;
        }

        private async Task<string?> ValidateScheduleConflictsAsync(Class cls, List<ClassSchedule> newSchedules)
        {
            if (cls.TeacherId == null)
            {
                return "Lớp chưa được gán giáo viên phụ trách.";
            }

            for (int i = 0; i < newSchedules.Count; i++)
            {
                if (!TryParseTimeSlot(newSchedules[i].TimeSlot, out var startA, out var endA))
                {
                    return "Giờ học phải có dạng HH:mm-HH:mm.";
                }

                if (startA >= endA)
                {
                    return "Giờ kết thúc phải lớn hơn giờ bắt đầu.";
                }

                for (int j = i + 1; j < newSchedules.Count; j++)
                {
                    if (newSchedules[i].DayOfWeek != newSchedules[j].DayOfWeek) continue;
                    if (!TryParseTimeSlot(newSchedules[j].TimeSlot, out var startB, out var endB))
                    {
                        return "Giờ học phải có dạng HH:mm-HH:mm.";
                    }

                    if (TimeRangesOverlap(startA, endA, startB, endB))
                    {
                        return $"Các ca học mới bị trùng giờ vào {newSchedules[i].DayOfWeek}.";
                    }
                }
            }

            return null;
        }

        private static bool TryParseTimeSlot(string timeSlot, out TimeOnly start, out TimeOnly end)
        {
            start = default;
            end = default;

            var parts = timeSlot.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            return TimeOnly.TryParse(parts[0], out start) && TimeOnly.TryParse(parts[1], out end);
        }

        private static bool TimeRangesOverlap(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB)
        {
            return startA < endB && startB < endA;
        }
    }
}
