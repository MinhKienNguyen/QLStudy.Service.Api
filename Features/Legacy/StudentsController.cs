using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Infrastructure.Data;
using QLStudy.Domain.Entities;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsControllerLogic : BaseApiController
    {
        public StudentsControllerLogic(QLStudyDbContext context) : base(context)
        {
        }

        public class StudentClassEnrollmentDto
        {
            public int Id { get; set; }
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string StartMonth { get; set; } = string.Empty;
            public string? EndMonth { get; set; }
            public string Status { get; set; } = string.Empty;
            public string? Reason { get; set; }
        }

        public class EnrollmentStatusDto
        {
            public string EffectiveMonth { get; set; } = string.Empty;
            public string? Reason { get; set; }
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
            public Dictionary<int, string> ClassStatuses { get; set; } = new();
            public List<StudentClassEnrollmentDto> Enrollments { get; set; } = new();
            public List<int> RewardIds { get; set; } = new();
            public List<string> RewardNames { get; set; } = new();
        }

        public class EnrollmentPlanDto
        {
            public string Status { get; set; } = "Active";
            public string? InactiveFromMonth { get; set; }
            public string? ResumeMonth { get; set; }
            public string? Reason { get; set; }
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
            public Dictionary<int, EnrollmentPlanDto> ClassEnrollmentPlans { get; set; } = new();
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
                .Include(s => s.StudentClassEnrollments)
                    .ThenInclude(e => e.Class)
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
                .Include(s => s.StudentClassEnrollments)
                    .ThenInclude(e => e.Class)
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
                .Include(s => s.StudentClassEnrollments)
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
            _context.StudentClassEnrollments.RemoveRange(student.StudentClassEnrollments);
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



        [HttpPost("{studentId}/classes/{classId}/pause")]
        public async Task<IActionResult> PauseClassEnrollment(int studentId, int classId, [FromBody] EnrollmentStatusDto dto)
        {
            return await CloseCurrentEnrollment(studentId, classId, dto, "Paused");
        }

        [HttpPost("{studentId}/classes/{classId}/stop")]
        public async Task<IActionResult> StopClassEnrollment(int studentId, int classId, [FromBody] EnrollmentStatusDto dto)
        {
            return await CloseCurrentEnrollment(studentId, classId, dto, "Stopped");
        }

        [HttpPost("{studentId}/classes/{classId}/resume")]
        public async Task<IActionResult> ResumeClassEnrollment(int studentId, int classId, [FromBody] EnrollmentStatusDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            var validationError = await ValidateSingleClassStartMonthAsync(classId, dto.EffectiveMonth);
            if (validationError != null) return BadRequest(new { message = validationError });

            var exists = await _context.Students.AnyAsync(s => s.Id == studentId) && await _context.Classes.AnyAsync(c => c.Id == classId);
            if (!exists) return NotFound();

            var currentActive = await _context.StudentClassEnrollments
                .Where(e => e.StudentId == studentId && e.ClassId == classId && e.Status == "Active")
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (currentActive == null)
            {
                _context.StudentClassEnrollments.Add(new StudentClassEnrollment
                {
                    StudentId = studentId,
                    ClassId = classId,
                    StartMonth = dto.EffectiveMonth.Trim().ToUpperInvariant(),
                    Status = "Active",
                    Reason = dto.Reason
                });
            }

            var relation = await _context.StudentClasses.FindAsync(studentId, classId);
            if (relation == null)
            {
                _context.StudentClasses.Add(new StudentClass
                {
                    StudentId = studentId,
                    ClassId = classId,
                    StartMonth = dto.EffectiveMonth.Trim().ToUpperInvariant()
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật học lại thành công." });
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
                return BadRequest(new { message = "KhÃ´ng thá»ƒ xoÃ¡ há»c sinh Ä‘Ã£ cÃ³ dá»¯ liá»‡u Ä‘iá»ƒm danh." });
            }

            var hasPaidTuition = await _context.TuitionPayments.AnyAsync(p => p.StudentId == id && p.AmountPaid > 0);
            if (hasPaidTuition)
            {
                return BadRequest(new { message = "KhÃ´ng thá»ƒ xoÃ¡ há»c sinh Ä‘Ã£ cÃ³ dá»¯ liá»‡u Ä‘Ã³ng há»c phÃ­." });
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
                ClassStatuses = student.StudentClasses.ToDictionary(sc => sc.ClassId, sc => GetCurrentEnrollmentStatus(student, sc.ClassId)),
                Enrollments = student.StudentClassEnrollments.OrderBy(e => e.ClassId).ThenBy(e => e.CreatedAt).Select(e => new StudentClassEnrollmentDto
                {
                    Id = e.Id,
                    ClassId = e.ClassId,
                    ClassName = e.Class?.Name ?? string.Empty,
                    StartMonth = e.StartMonth,
                    EndMonth = e.EndMonth,
                    Status = e.Status,
                    Reason = e.Reason
                }).ToList(),
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

                AddEnrollmentPlan(studentId, classId, GetClassStartMonth(dto, classId), dto.ClassEnrollmentPlans.TryGetValue(classId, out var plan) ? plan : null);
            }

            foreach (var rewardId in dto.RewardIds.Distinct())
            {
                _context.StudentRewards.Add(new StudentReward { StudentId = studentId, RewardOptionId = rewardId });
            }
        }



        private void AddEnrollmentPlan(int studentId, int classId, string startMonth, EnrollmentPlanDto? plan)
        {
            var status = NormalizeEnrollmentStatus(plan?.Status);
            var reason = plan?.Reason;

            if (status == "Active")
            {
                AddEnrollment(studentId, classId, startMonth, null, "Active", reason);
                return;
            }

            if (!TryParseMonth(plan?.InactiveFromMonth ?? string.Empty, out var inactiveFromMonth))
            {
                AddEnrollment(studentId, classId, startMonth, null, "Active", reason);
                return;
            }

            AddEnrollment(studentId, classId, startMonth, FormatMonth(PreviousMonth(inactiveFromMonth)), status, reason);

            if (!string.IsNullOrWhiteSpace(plan?.ResumeMonth))
            {
                AddEnrollment(studentId, classId, plan!.ResumeMonth!.Trim().ToUpperInvariant(), null, "Active", reason);
            }
        }

        private void AddEnrollment(int studentId, int classId, string startMonth, string? endMonth, string status, string? reason)
        {
            _context.StudentClassEnrollments.Add(new StudentClassEnrollment
            {
                StudentId = studentId,
                ClassId = classId,
                StartMonth = startMonth,
                EndMonth = endMonth,
                Status = status,
                Reason = reason,
                EndedAt = status == "Active" ? null : DateTime.UtcNow
            });
        }

        private static string NormalizeEnrollmentStatus(string? status)
        {
            return status == "Paused" || status == "Stopped" ? status : "Active";
        }

        private async Task<IActionResult> CloseCurrentEnrollment(int studentId, int classId, EnrollmentStatusDto dto, string status)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();
            if (user.Role != "Manager") return Forbid();

            if (!TryParseMonth(dto.EffectiveMonth, out var effectiveMonth))
            {
                return BadRequest(new { message = "Vui lòng chọn tháng bắt đầu nghỉ hợp lệ, ví dụ T8." });
            }

            var enrollment = await _context.StudentClassEnrollments
                .Where(e => e.StudentId == studentId && e.ClassId == classId && e.Status == "Active")
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (enrollment == null) return NotFound();

            enrollment.Status = status;
            enrollment.EndMonth = FormatMonth(PreviousMonth(effectiveMonth));
            enrollment.Reason = dto.Reason;
            enrollment.EndedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { message = status == "Paused" ? "Đã tạm nghỉ học sinh khỏi lớp." : "Đã cho học sinh nghỉ hẳn khỏi lớp." });
        }

        private async Task<string?> ValidateSingleClassStartMonthAsync(int classId, string startMonth)
        {
            return await ValidateStudentStartMonthsAsync(new Dictionary<int, string> { [classId] = startMonth }, new List<int> { classId }, startMonth);
        }

        private static string GetCurrentEnrollmentStatus(Student student, int classId)
        {
            return student.StudentClassEnrollments
                .Where(e => e.ClassId == classId)
                .OrderByDescending(e => e.Status == "Active")
                .ThenByDescending(e => e.CreatedAt)
                .FirstOrDefault()?.Status ?? "Active";
        }

        private static int PreviousMonth(int month)
        {
            return month <= 1 ? 12 : month - 1;
        }

        private static string FormatMonth(int month)
        {
            return $"T{month}";
        }

        private async Task<bool> StudentExists(int id)
        {
            return await _context.Students.AnyAsync(e => e.Id == id);
        }

        private async Task<string?> ValidateStudentStartMonthsAsync(Dictionary<int, string> classStartMonths, List<int> classIds, string fallbackStartMonth)
        {
            if (classIds == null || !classIds.Any())
            {
                return "Vui lÃ²ng chá»n Ã­t nháº¥t má»™t lá»›p há»c.";
            }

            var distinctClassIds = classIds.Distinct().ToList();
            var classes = await _context.Classes
                .Where(c => distinctClassIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.StartDate })
                .ToListAsync();

            if (classes.Count != distinctClassIds.Count)
            {
                return "Má»™t hoáº·c nhiá»u lá»›p há»c khÃ´ng tá»“n táº¡i.";
            }

            foreach (var cls in classes)
            {
                if (!classStartMonths.TryGetValue(cls.Id, out var startMonth) || string.IsNullOrWhiteSpace(startMonth))
                {
                    startMonth = fallbackStartMonth;
                }

                if (!TryParseMonth(startMonth, out var studentMonth))
                {
                    return $"Vui lÃ²ng chá»n thÃ¡ng báº¯t Ä‘áº§u há»c cho lá»›p {cls.Name}.";
                }

                var classMonth = cls.StartDate?.Month ?? 1;
                if (studentMonth < classMonth)
                {
                    return $"ThÃ¡ng báº¯t Ä‘áº§u há»c cá»§a lá»›p {cls.Name} khÃ´ng Ä‘Æ°á»£c nhá» hÆ¡n T{classMonth}.";
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

