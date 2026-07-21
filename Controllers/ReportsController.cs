using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : BaseApiController
    {
        public ReportsController(QLStudyDbContext context) : base(context)
        {
        }

        // GET: api/reports/semesters-summary
        [HttpGet("semesters-summary")]
        public async Task<IActionResult> GetSemestersSummary()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                var teacherSummary = await _context.Semesters
                    .Select(s => new
                    {
                        semesterId = s.Id,
                        semesterName = s.Name,
                        totalClasses = s.Classes.Where(c => c.SubjectId != null && subjectIds.Contains(c.SubjectId.Value)).Count(),
                        totalStudents = _context.StudentClasses
                            .Where(sc => sc.Class!.SemesterId == s.Id && sc.Class.SubjectId != null && subjectIds.Contains(sc.Class.SubjectId.Value))
                            .Select(sc => sc.StudentId)
                            .Distinct()
                            .Count(),
                        totalRevenue = _context.TuitionPayments
                            .Where(p => p.TuitionPeriod!.SemesterId == s.Id && p.Class!.SubjectId != null && subjectIds.Contains(p.Class.SubjectId.Value))
                            .Sum(p => (decimal?)p.AmountPaid) ?? 0
                    })
                    .ToListAsync();

                return Ok(teacherSummary);
            }

            var summary = await _context.Semesters
                .Select(s => new
                {
                    semesterId = s.Id,
                    semesterName = s.Name,
                    totalClasses = s.Classes.Count,
                    totalStudents = _context.StudentClasses
                        .Where(sc => sc.Class!.SemesterId == s.Id)
                        .Select(sc => sc.StudentId)
                        .Distinct()
                        .Count(),
                    totalRevenue = _context.TuitionPayments
                        .Where(p => p.TuitionPeriod!.SemesterId == s.Id)
                        .Sum(p => (decimal?)p.AmountPaid) ?? 0
                })
                .ToListAsync();

            return Ok(summary);
        }

        // GET: api/reports/monthly-revenue?semesterId=5
        [HttpGet("monthly-revenue")]
        public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int semesterId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var semesterExists = await _context.Semesters.AnyAsync(s => s.Id == semesterId);
            if (!semesterExists) return NotFound("Semester not found");

            var periods = await _context.TuitionPeriods
                .Where(p => p.SemesterId == semesterId)
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
        public async Task<IActionResult> GetClassRevenue([FromQuery] int semesterId)
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

            var classRevenue = new List<object>();
            foreach (var c in classes)
            {
                var amount = await _context.TuitionPayments
                    .Where(tp => tp.ClassId == c.Id)
                    .SumAsync(tp => (decimal?)tp.AmountPaid) ?? 0;

                var studentCount = await _context.StudentClasses
                    .CountAsync(sc => sc.ClassId == c.Id);

                classRevenue.Add(new
                {
                    classId = c.Id,
                    className = c.Name,
                    studentCount = studentCount,
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
    }
}
