using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Infrastructure.Data;
using QLStudy.Domain.Entities;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClassesControllerLogic : BaseApiController
    {
        public ClassesControllerLogic(QLStudyDbContext context) : base(context)
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
                return BadRequest(new { message = "Lá»›p há»c Ä‘Ã£ cÃ³ há»c sinh nÃªn khÃ´ng thá»ƒ xÃ³a." });
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
            if (teacherId == null) return "Vui lÃ²ng chá»n giÃ¡o viÃªn phá»¥ trÃ¡ch lá»›p.";
            if (subjectId == null) return "Vui lÃ²ng chá»n mÃ´n há»c cho lá»›p.";

            var teacher = await _context.Users
                .Include(u => u.UserSubjects)
                .FirstOrDefaultAsync(u => u.Id == teacherId.Value && u.Role == "Teacher" && u.Status == "Active");

            if (teacher == null)
            {
                return "GiÃ¡o viÃªn khÃ´ng tá»“n táº¡i hoáº·c Ä‘ang bá»‹ khÃ³a.";
            }

            if (!teacher.UserSubjects.Any(us => us.SubjectId == subjectId.Value))
            {
                return "MÃ´n há»c Ä‘Ã£ chá»n khÃ´ng thuá»™c pháº¡m vi mÃ´n cá»§a giÃ¡o viÃªn nÃ y.";
            }

            return null;
        }

        private static string? ValidateClassDateRange(DateOnly? startDate, DateOnly? endDate)
        {
            if (startDate != null && !IsReasonableClassDate(startDate.Value))
            {
                return "Ngày bắt đầu lớp học không hợp lệ. Vui lòng chọn năm từ 2000 đến 2100.";
            }

            if (endDate != null && !IsReasonableClassDate(endDate.Value))
            {
                return "Ngày kết thúc lớp học không hợp lệ. Vui lòng chọn năm từ 2000 đến 2100.";
            }

            if (startDate != null && endDate != null && endDate < startDate)
            {
                return "Ngày kết thúc lớp học phải lớn hơn hoặc bằng ngày bắt đầu.";
            }

            if (startDate != null && endDate != null && MonthDistance(startDate.Value, endDate.Value) > 36)
            {
                return "Thời gian lớp học không được vượt quá 36 tháng.";
            }

            return null;
        }

        private static bool IsReasonableClassDate(DateOnly value)
        {
            return value.Year >= 2000 && value.Year <= 2100;
        }

        private static int MonthDistance(DateOnly start, DateOnly end)
        {
            return (end.Year - start.Year) * 12 + end.Month - start.Month;
        }

        private async Task<string?> ValidateScheduleConflictsAsync(Class cls, List<ClassSchedule> newSchedules)
        {
            if (cls.TeacherId == null)
            {
                return "Lá»›p chÆ°a Ä‘Æ°á»£c gÃ¡n giÃ¡o viÃªn phá»¥ trÃ¡ch.";
            }

            for (int i = 0; i < newSchedules.Count; i++)
            {
                if (!TryParseTimeSlot(newSchedules[i].TimeSlot, out var startA, out var endA))
                {
                    return "Giá» há»c pháº£i cÃ³ dáº¡ng HH:mm-HH:mm.";
                }

                if (startA >= endA)
                {
                    return "Giá» káº¿t thÃºc pháº£i lá»›n hÆ¡n giá» báº¯t Ä‘áº§u.";
                }

                for (int j = i + 1; j < newSchedules.Count; j++)
                {
                    if (newSchedules[i].DayOfWeek != newSchedules[j].DayOfWeek) continue;
                    if (!TryParseTimeSlot(newSchedules[j].TimeSlot, out var startB, out var endB))
                    {
                        return "Giá» há»c pháº£i cÃ³ dáº¡ng HH:mm-HH:mm.";
                    }

                    if (TimeRangesOverlap(startA, endA, startB, endB))
                    {
                        return $"CÃ¡c ca há»c má»›i bá»‹ trÃ¹ng giá» vÃ o {newSchedules[i].DayOfWeek}.";
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

