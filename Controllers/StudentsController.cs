using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsController : BaseApiController
    {
        public StudentsController(QLStudyDbContext context) : base(context)
        {
        }

        public class StudentResponseDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
            public string StartMonth { get; set; } = string.Empty;
            public List<int> ClassIds { get; set; } = new();
            public List<string> ClassNames { get; set; } = new();
            public Dictionary<int, string> ClassStartMonths { get; set; } = new();
            public List<int> RewardIds { get; set; } = new();
            public List<string> RewardNames { get; set; } = new();
        }

        public class StudentSaveDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
            public string StartMonth { get; set; } = string.Empty;
            public List<int> ClassIds { get; set; } = new();
            public Dictionary<int, string> ClassStartMonths { get; set; } = new();
            public List<int> RewardIds { get; set; } = new();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StudentResponseDto>>> GetStudents(int? classId, int? semesterId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var query = _context.Students
                .Include(s => s.StudentClasses)
                    .ThenInclude(sc => sc.Class)
                .Include(s => s.StudentRewards)
                    .ThenInclude(sr => sr.RewardOption)
                .AsQueryable();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                if (classId.HasValue)
                {
                    var cls = await _context.Classes.FindAsync(classId.Value);
                    if (cls == null || cls.SubjectId == null || !subjectIds.Contains(cls.SubjectId.Value))
                    {
                        return Ok(new List<StudentResponseDto>());
                    }
                    query = query.Where(s => s.StudentClasses.Any(sc => sc.ClassId == classId.Value));
                }
                else
                {
                    query = query.Where(s => s.StudentClasses.Any(sc => sc.Class != null && sc.Class.SubjectId != null && subjectIds.Contains(sc.Class.SubjectId.Value)));
                }
            }
            else if (classId.HasValue)
            {
                query = query.Where(s => s.StudentClasses.Any(sc => sc.ClassId == classId.Value));
            }
            else if (semesterId.HasValue)
            {
                query = query.Where(s => s.StudentClasses.Any(sc => sc.Class!.SemesterId == semesterId.Value));
            }

            var students = await query.OrderBy(s => s.Name).ToListAsync();
            return Ok(students.Select(ToDto).ToList());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StudentResponseDto>> GetStudent(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var student = await _context.Students
                .Include(s => s.StudentClasses)
                    .ThenInclude(sc => sc.Class)
                .Include(s => s.StudentRewards)
                    .ThenInclude(sr => sr.RewardOption)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null) return NotFound();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                var hasAccess = student.StudentClasses.Any(sc => sc.Class != null && sc.Class.SubjectId != null && subjectIds.Contains(sc.Class.SubjectId.Value));
                if (!hasAccess) return Forbid();
            }

            return Ok(ToDto(student));
        }

        [HttpPost]
        public async Task<ActionResult<Student>> CreateStudent([FromBody] StudentSaveDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var validationError = await ValidateStudentStartMonthsAsync(dto.ClassStartMonths, dto.ClassIds, dto.StartMonth);
            if (validationError != null) return BadRequest(new { message = validationError });

            dto.StartMonth = GetPrimaryStartMonth(dto);
            var student = new Student
            {
                Name = dto.Name,
                PhoneNumber = dto.PhoneNumber,
                Email = dto.Email,
                StartMonth = dto.StartMonth
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            AddStudentRelations(student.Id, dto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStudent), new { id = student.Id }, student);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentSaveDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();
            if (id != dto.Id) return BadRequest();

            var student = await _context.Students
                .Include(s => s.StudentClasses)
                .Include(s => s.StudentRewards)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null) return NotFound();

            var validationError = await ValidateStudentStartMonthsAsync(dto.ClassStartMonths, dto.ClassIds, dto.StartMonth);
            if (validationError != null) return BadRequest(new { message = validationError });

            dto.StartMonth = GetPrimaryStartMonth(dto);
            student.Name = dto.Name;
            student.PhoneNumber = dto.PhoneNumber;
            student.Email = dto.Email;
            student.StartMonth = dto.StartMonth;

            _context.StudentClasses.RemoveRange(student.StudentClasses);
            _context.StudentRewards.RemoveRange(student.StudentRewards);
            AddStudentRelations(student.Id, dto);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await StudentExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            var hasAttendance = await _context.Attendances.AnyAsync(a => a.StudentId == id);
            if (hasAttendance)
            {
                return BadRequest(new { message = "Không thể xoá học sinh đã có dữ liệu điểm danh." });
            }

            var hasPaidTuition = await _context.TuitionPayments.AnyAsync(p => p.StudentId == id && p.AmountPaid > 0);
            if (hasPaidTuition)
            {
                return BadRequest(new { message = "Không thể xoá học sinh đã có dữ liệu đóng học phí." });
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static StudentResponseDto ToDto(Student student)
        {
            return new StudentResponseDto
            {
                Id = student.Id,
                Name = student.Name,
                PhoneNumber = student.PhoneNumber,
                Email = student.Email,
                StartMonth = student.StartMonth,
                ClassIds = student.StudentClasses.Select(sc => sc.ClassId).ToList(),
                ClassNames = student.StudentClasses.Select(sc => sc.Class!.Name).ToList(),
                ClassStartMonths = student.StudentClasses.ToDictionary(sc => sc.ClassId, sc => string.IsNullOrWhiteSpace(sc.StartMonth) ? student.StartMonth : sc.StartMonth),
                RewardIds = student.StudentRewards.Select(sr => sr.RewardOptionId).ToList(),
                RewardNames = student.StudentRewards.Select(sr => sr.RewardOption!.Name).ToList()
            };
        }

        private void AddStudentRelations(int studentId, StudentSaveDto dto)
        {
            foreach (var classId in dto.ClassIds.Distinct())
            {
                _context.StudentClasses.Add(new StudentClass
                {
                    StudentId = studentId,
                    ClassId = classId,
                    StartMonth = GetClassStartMonth(dto, classId)
                });
            }

            foreach (var rewardId in dto.RewardIds.Distinct())
            {
                _context.StudentRewards.Add(new StudentReward { StudentId = studentId, RewardOptionId = rewardId });
            }
        }

        private async Task<bool> StudentExists(int id)
        {
            return await _context.Students.AnyAsync(e => e.Id == id);
        }

        private async Task<string?> ValidateStudentStartMonthsAsync(Dictionary<int, string> classStartMonths, List<int> classIds, string fallbackStartMonth)
        {
            if (classIds == null || !classIds.Any())
            {
                return "Vui lòng chọn ít nhất một lớp học.";
            }

            var distinctClassIds = classIds.Distinct().ToList();
            var classes = await _context.Classes
                .Where(c => distinctClassIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.StartDate })
                .ToListAsync();

            if (classes.Count != distinctClassIds.Count)
            {
                return "Một hoặc nhiều lớp học không tồn tại.";
            }

            foreach (var cls in classes)
            {
                if (!classStartMonths.TryGetValue(cls.Id, out var startMonth) || string.IsNullOrWhiteSpace(startMonth))
                {
                    startMonth = fallbackStartMonth;
                }

                if (!TryParseMonth(startMonth, out var studentMonth))
                {
                    return $"Vui lòng chọn tháng bắt đầu học cho lớp {cls.Name}.";
                }

                var classMonth = cls.StartDate?.Month ?? 1;
                if (studentMonth < classMonth)
                {
                    return $"Tháng bắt đầu học của lớp {cls.Name} không được nhỏ hơn T{classMonth}.";
                }
            }

            return null;
        }

        private static string GetClassStartMonth(StudentSaveDto dto, int classId)
        {
            return dto.ClassStartMonths.TryGetValue(classId, out var month) && !string.IsNullOrWhiteSpace(month)
                ? month.Trim().ToUpperInvariant()
                : dto.StartMonth;
        }

        private static string GetPrimaryStartMonth(StudentSaveDto dto)
        {
            var firstClassId = dto.ClassIds.FirstOrDefault();
            return firstClassId > 0 ? GetClassStartMonth(dto, firstClassId) : dto.StartMonth;
        }

        private static bool TryParseMonth(string value, out int month)
        {
            month = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var normalized = value.Trim().ToUpperInvariant();
            if (normalized.StartsWith("T"))
            {
                normalized = normalized[1..];
            }

            return int.TryParse(normalized, out month) && month >= 1 && month <= 12;
        }
    }
}
