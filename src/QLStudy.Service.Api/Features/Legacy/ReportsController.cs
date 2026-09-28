using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Infrastructure.Data;
using QLStudy.Domain.Entities;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsControllerLogic : BaseApiController
    {
        public ReportsControllerLogic(QLStudyDbContext context) : base(context)
        {
        }

        // GET: api/reports/semesters-summary
        [HttpGet("semesters-summary")]
        public async Task<IActionResult> GetSemestersSummary()
        {
            var userSubjects = await _context.UserSubjects
                .Include(us => us.User)
                .Include(us => us.Subject)
                .ToListAsync();

            var debugList = userSubjects.Select(us => new {
                userId = us.UserId,
                userName = us.User?.FullName,
                userEmail = us.User?.Email,
                userRole = us.User?.Role,
                subjectId = us.SubjectId,
                subjectName = us.Subject?.Name
            }).ToList();

            return Ok(debugList);
        }

        // GET: api/reports/monthly-revenue?semesterId=5
        [HttpGet("monthly-revenue")]
        public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int semesterId, [FromQuery] int? fromPeriodId = null, [FromQuery] int? toPeriodId = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var semesterExists = await _context.Semesters.AnyAsync(s => s.Id == semesterId);
            if (!semesterExists) return NotFound("Semester not found");

            var fromDisplayOrder = fromPeriodId.HasValue
                ? await _context.TuitionPeriods.Where(p => p.SemesterId == semesterId && p.Id == fromPeriodId.Value).Select(p => (int?)p.DisplayOrder).FirstOrDefaultAsync()
                : null;
            var toDisplayOrder = toPeriodId.HasValue
                ? await _context.TuitionPeriods.Where(p => p.SemesterId == semesterId && p.Id == toPeriodId.Value).Select(p => (int?)p.DisplayOrder).FirstOrDefaultAsync()
                : null;

            var periods = await _context.TuitionPeriods
                .Where(p => p.SemesterId == semesterId)
                .Where(p => !fromDisplayOrder.HasValue || p.DisplayOrder >= fromDisplayOrder.Value)
                .Where(p => !toDisplayOrder.HasValue || p.DisplayOrder <= toDisplayOrder.Value)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            var monthlyRevenue = new List<object>();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                foreach (var p in periods)
                {
                    var totalAmount = await _context.TuitionPayments
                        .Where(tp => tp.TuitionPeriodId == p.Id && tp.Class!.SubjectId != null && subjectIds.Contains(tp.Class.SubjectId.Value))
                        .SumAsync(tp => (decimal?)tp.AmountPaid) ?? 0;

                    monthlyRevenue.Add(new
                    {
                        periodId = p.Id,
                        monthName = p.MonthName,
                        amount = totalAmount
                    });
                }
            }
            else
            {
                foreach (var p in periods)
                {
                    var totalAmount = await _context.TuitionPayments
                        .Where(tp => tp.TuitionPeriodId == p.Id)
                        .SumAsync(tp => (decimal?)tp.AmountPaid) ?? 0;

                    monthlyRevenue.Add(new
                    {
                        periodId = p.Id,
                        monthName = p.MonthName,
                        amount = totalAmount
                    });
                }
            }

            return Ok(monthlyRevenue);
        }

        // GET: api/reports/class-revenue?semesterId=5
        [HttpGet("class-revenue")]
        public async Task<IActionResult> GetClassRevenue([FromQuery] int semesterId, [FromQuery] int? fromPeriodId = null, [FromQuery] int? toPeriodId = null)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var semesterExists = await _context.Semesters.AnyAsync(s => s.Id == semesterId);
            if (!semesterExists) return NotFound("Semester not found");

            var query = _context.Classes.Where(c => c.SemesterId == semesterId);

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                query = query.Where(c => c.SubjectId != null && subjectIds.Contains(c.SubjectId.Value));
            }

            var classes = await query.OrderBy(c => c.Name).ToListAsync();

            var fromDisplayOrder = fromPeriodId.HasValue
                ? await _context.TuitionPeriods.Where(p => p.SemesterId == semesterId && p.Id == fromPeriodId.Value).Select(p => (int?)p.DisplayOrder).FirstOrDefaultAsync()
                : null;
            var toDisplayOrder = toPeriodId.HasValue
                ? await _context.TuitionPeriods.Where(p => p.SemesterId == semesterId && p.Id == toPeriodId.Value).Select(p => (int?)p.DisplayOrder).FirstOrDefaultAsync()
                : null;

            var periodsInRange = await _context.TuitionPeriods
                .Where(p => p.SemesterId == semesterId)
                .Where(p => !fromDisplayOrder.HasValue || p.DisplayOrder >= fromDisplayOrder.Value)
                .Where(p => !toDisplayOrder.HasValue || p.DisplayOrder <= toDisplayOrder.Value)
                .ToListAsync();

            var periodIds = periodsInRange.Select(p => p.Id).ToList();

            var allowedClassIds = classes.Select(c => c.Id).ToList();
            var enrollments = await _context.StudentClassEnrollments
                .Where(e => allowedClassIds.Contains(e.ClassId))
                .ToListAsync();

            var studentClasses = await _context.StudentClasses
                .Include(sc => sc.Student)
                .Where(sc => allowedClassIds.Contains(sc.ClassId))
                .ToListAsync();

            var enrollmentLookup = enrollments
                .GroupBy(e => $"{e.StudentId}:{e.ClassId}")
                .ToDictionary(g => g.Key, g => g.ToList());

            var classRevenue = new List<object>();
            foreach (var c in classes)
            {
                var amount = await _context.TuitionPayments
                    .Where(tp => tp.ClassId == c.Id && periodIds.Contains(tp.TuitionPeriodId))
                    .SumAsync(tp => (decimal?)tp.AmountPaid) ?? 0;

                var classStudents = studentClasses.Where(sc => sc.ClassId == c.Id).ToList();
                int activeStudentCount = 0;

                foreach (var sc in classStudents)
                {
                    var lookupKey = $"{sc.StudentId}:{sc.ClassId}";
                    var rowEnrollments = enrollmentLookup.TryGetValue(lookupKey, out var foundEnrollments)
                        ? foundEnrollments
                        : new List<StudentClassEnrollment>();

                    var fallbackStart = string.IsNullOrWhiteSpace(sc.StartMonth) ? sc.Student!.StartMonth : sc.StartMonth;

                    bool isActiveInRange = false;
                    foreach (var period in periodsInRange)
                    {
                        if (IsStudentActiveInPeriod(period, c.StartDate, c.EndDate, rowEnrollments, fallbackStart))
                        {
                            isActiveInRange = true;
                            break;
                        }
                    }

                    if (isActiveInRange)
                    {
                        activeStudentCount++;
                    }
                }

                classRevenue.Add(new
                {
                    classId = c.Id,
                    className = c.Name,
                    studentCount = activeStudentCount,
                    amount = amount
                });
            }

            return Ok(classRevenue);
        }

        // GET: api/reports/payment-status?semesterId=5&periodId=10
        [HttpGet("payment-status")]
        public async Task<IActionResult> GetPaymentStatus([FromQuery] int semesterId, [FromQuery] int periodId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var period = await _context.TuitionPeriods.FindAsync(periodId);
            if (period == null || period.SemesterId != semesterId)
            {
                return BadRequest("Invalid period or semester ID");
            }

            // Get all student-class enrollments in this semester
            var queryStudentClasses = _context.StudentClasses
                .Include(sc => sc.Student)
                .Include(sc => sc.Class)
                .Where(sc => sc.Class!.SemesterId == semesterId);

            var queryPayments = _context.TuitionPayments
                .Where(p => p.TuitionPeriodId == periodId);

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                queryStudentClasses = queryStudentClasses.Where(sc => sc.Class!.SubjectId != null && subjectIds.Contains(sc.Class!.SubjectId.Value));
                queryPayments = queryPayments.Where(p => p.Class!.SubjectId != null && subjectIds.Contains(p.Class!.SubjectId.Value));
            }

            var studentClasses = await queryStudentClasses
                .OrderBy(sc => sc.Class!.Name)
                    .ThenBy(sc => sc.Student!.Name)
                .ToListAsync();

            var allowedClassIds = studentClasses.Select(sc => sc.ClassId).Distinct().ToList();
            var allowedStudentIds = studentClasses.Select(sc => sc.StudentId).Distinct().ToList();
            var enrollments = await _context.StudentClassEnrollments
                .Where(e => allowedClassIds.Contains(e.ClassId) && allowedStudentIds.Contains(e.StudentId))
                .ToListAsync();

            var enrollmentLookup = enrollments
                .GroupBy(e => $"{e.StudentId}:{e.ClassId}")
                .ToDictionary(g => g.Key, g => g.ToList());

            var payments = await queryPayments.ToListAsync();
            var adjustments = await _context.TuitionAdjustments
                .Where(a => a.TuitionPeriodId == periodId)
                .ToListAsync();

            // Create dictionary for lookup
            var paymentDict = payments
                .GroupBy(p => new { p.StudentId, p.ClassId })
                .ToDictionary(g => g.Key, g => g.Last());
            var adjustmentDict = adjustments
                .GroupBy(a => new { a.StudentId, a.ClassId })
                .ToDictionary(g => g.Key, g => g.Last());

            var paidList = new List<object>();
            var unpaidList = new List<object>();

            foreach (var sc in studentClasses)
            {
                var lookupKey = $"{sc.StudentId}:{sc.ClassId}";
                var rowEnrollments = enrollmentLookup.TryGetValue(lookupKey, out var foundEnrollments)
                    ? foundEnrollments
                    : new List<StudentClassEnrollment>();

                var fallbackStart = string.IsNullOrWhiteSpace(sc.StartMonth) ? sc.Student!.StartMonth : sc.StartMonth;
                if (!IsStudentActiveInPeriod(period, sc.Class!.StartDate, sc.Class!.EndDate, rowEnrollments, fallbackStart))
                {
                    continue;
                }

                var key = new { sc.StudentId, sc.ClassId };
                var adjustment = adjustmentDict.TryGetValue(key, out var adjustmentValue) ? adjustmentValue : null;
                var amountDue = adjustment == null
                    ? sc.Class!.TuitionFee
                    : CalculateAdjustedTuition(sc.Class!.TuitionFee, adjustment.AdjustmentType, adjustment.AdjustmentValue);

                if (paymentDict.TryGetValue(key, out var payment) && payment.AmountPaid > 0)
                {
                    paidList.Add(new
                    {
                        studentId = sc.StudentId,
                        studentName = sc.Student!.Name,
                        classId = sc.ClassId,
                        className = sc.Class!.Name,
                        amountDue,
                        amountPaid = payment.AmountPaid,
                        notes = payment.Notes,
                        paidAt = payment.PaidAt,
                        adjustmentType = adjustment?.AdjustmentType ?? "None",
                        adjustmentValue = adjustment?.AdjustmentValue ?? 0,
                        adjustmentNote = adjustment?.Note ?? string.Empty
                    });
                }
                else if (amountDue <= 0)
                {
                    paidList.Add(new
                    {
                        studentId = sc.StudentId,
                        studentName = sc.Student!.Name,
                        classId = sc.ClassId,
                        className = sc.Class!.Name,
                        amountDue,
                        amountPaid = 0,
                        notes = string.IsNullOrWhiteSpace(adjustment?.Note) ? "Miễn học phí" : adjustment!.Note,
                        paidAt = (DateTime?)null,
                        adjustmentType = adjustment?.AdjustmentType ?? "Free",
                        adjustmentValue = adjustment?.AdjustmentValue ?? 0,
                        adjustmentNote = adjustment?.Note ?? string.Empty,
                        isWaived = true
                    });
                }
                else
                {
                    unpaidList.Add(new
                    {
                        studentId = sc.StudentId,
                        studentName = sc.Student!.Name,
                        classId = sc.ClassId,
                        className = sc.Class!.Name,
                        amountDue,
                        adjustmentType = adjustment?.AdjustmentType ?? "None",
                        adjustmentValue = adjustment?.AdjustmentValue ?? 0,
                        adjustmentNote = adjustment?.Note ?? string.Empty
                    });
                }
            }

            return Ok(new
            {
                paid = paidList,
                unpaid = unpaidList
            });
        }

        private static decimal CalculateAdjustedTuition(decimal standardFee, string adjustmentType, decimal adjustmentValue)
        {
            var amountDue = adjustmentType switch
            {
                "DiscountPercent" => standardFee * (100 - Math.Min(100, Math.Max(0, adjustmentValue))) / 100,
                "DiscountAmount" => standardFee - Math.Max(0, adjustmentValue),
                "FixedAmount" => Math.Max(0, adjustmentValue),
                "Free" => 0,
                _ => standardFee
            };

            return Math.Round(Math.Max(0, amountDue), 0);
        }

        private static bool IsStudentActiveInPeriod(TuitionPeriod period, DateOnly? classStartDate, DateOnly? classEndDate, List<StudentClassEnrollment> enrollments, string fallbackStartMonth)
        {
            if (!TryParsePeriodStart(period.MonthName, out var targetDate))
            {
                return true;
            }

            if (enrollments == null || !enrollments.Any())
            {
                var start = ResolveMonthForClassRange(fallbackStartMonth, classStartDate, classEndDate);
                if (start == null) return true;
                return targetDate >= start.Value;
            }

            foreach (var enrollment in enrollments)
            {
                var start = ResolveMonthForClassRange(enrollment.StartMonth, classStartDate, classEndDate);
                var end = string.IsNullOrWhiteSpace(enrollment.EndMonth)
                    ? null
                    : ResolveMonthForClassRange(enrollment.EndMonth!, classStartDate, classEndDate);

                if (start == null) continue;
                if (targetDate >= start.Value && (end == null || targetDate <= end.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParsePeriodStart(string value, out DateOnly periodStart)
        {
            periodStart = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var normalized = value.Trim().ToUpperInvariant();
            if (!normalized.StartsWith("T")) return false;

            var parts = normalized[1..].Split('/', '-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;

            if (!int.TryParse(parts[0], out var month) || month < 1 || month > 12) return false;
            if (!int.TryParse(parts[1], out var year) || year < 1) return false;

            periodStart = new DateOnly(year, month, 1);
            return true;
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
            normalized = normalized.Split('/', '-', StringSplitOptions.RemoveEmptyEntries)[0];

            return int.TryParse(normalized, out month) && month >= 1 && month <= 12;
        }

        private static DateOnly? ResolveMonthForClassRange(string monthValue, DateOnly? classStartDate, DateOnly? classEndDate)
        {
            if (TryParsePeriodStart(monthValue, out var explicitMonth)) return explicitMonth;
            if (!TryParseMonth(monthValue, out var month)) return null;

            var baseStart = classStartDate == null
                ? new DateOnly(DateTime.UtcNow.Year, month, 1)
                : new DateOnly(classStartDate.Value.Year, classStartDate.Value.Month, 1);
            var candidate = FirstMonthOnOrAfter(baseStart, month);
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

        private static DateOnly FirstMonthOnOrAfter(DateOnly baseStart, int month)
        {
            var candidate = new DateOnly(baseStart.Year, month, 1);
            if (candidate < baseStart)
            {
                candidate = candidate.AddYears(1);
            }

            return candidate;
        }
    }
}




