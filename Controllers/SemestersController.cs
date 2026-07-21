using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SemestersController : BaseApiController
    {
        public SemestersController(QLStudyDbContext context) : base(context)
        {
        }

        // GET: api/semesters
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Semester>>> GetSemesters()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            return await _context.Semesters
                .OrderBy(s => s.Id)
                .ToListAsync();
        }

        // GET: api/semesters/{id}/schedule
        [HttpGet("{id}/schedule")]
        public async Task<IActionResult> GetSchedule(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var semester = await _context.Semesters.FindAsync(id);
            if (semester == null) return NotFound();

            var schedules = await _context.ClassSchedules
                .Include(cs => cs.Class)
                    .ThenInclude(c => c!.Teacher)
                .Where(cs => cs.Class!.SemesterId == id)
                .ToListAsync();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                schedules = schedules.Where(cs => cs.Class != null && cs.Class.SubjectId != null && subjectIds.Contains(cs.Class.SubjectId.Value)).ToList();
            }

            // Find all unique timeslots
            var timeSlots = schedules
                .Select(s => s.TimeSlot)
                .Distinct()
                .OrderBy(GetTimeSlotSortValue)
                .ToList();

            var dayNames = new[] { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };
            var grid = new List<object>();

            foreach (var slot in timeSlots)
            {
                var dayDict = new Dictionary<string, object?>();
                dayDict["timeSlot"] = slot;

                foreach (var day in dayNames)
                {
                    var daySchedules = schedules
                        .Where(s => s.TimeSlot == slot && s.DayOfWeek == day)
                        .OrderBy(s => s.Class!.Name)
                        .Select(s => new
                        {
                            classId = s.ClassId,
                            className = s.Class!.Name,
                            teacherName = s.Class.Teacher != null ? s.Class.Teacher.FullName : null
                        })
                        .ToList();

                    dayDict[day] = daySchedules;
                }

                grid.Add(dayDict);
            }

            return Ok(grid);
        }

        private static int GetTimeSlotSortValue(string timeSlot)
        {
            var parts = timeSlot.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return int.MaxValue;

            var normalized = parts[0]
                .Replace("h", ":", StringComparison.OrdinalIgnoreCase)
                .Replace("H", ":", StringComparison.OrdinalIgnoreCase);

            if (normalized.EndsWith(":"))
            {
                normalized += "00";
            }

            if (TimeOnly.TryParse(normalized, out var start))
            {
                return start.Hour * 60 + start.Minute;
            }

            return int.MaxValue;
        }

        // GET: api/semesters/{id}/tuition-matrix
        [HttpGet("{id}/tuition-matrix")]
        public async Task<IActionResult> GetTuitionMatrix(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var semester = await _context.Semesters.FindAsync(id);
            if (semester == null) return NotFound();

            await EnsureTuitionPeriodsForClassDateRanges(id);

            // Get columns (Periods)
            var periods = await _context.TuitionPeriods
                .Where(p => p.SemesterId == id)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new { p.Id, p.MonthName, p.DisplayOrder })
                .ToListAsync();

            // Get rows (Students)
            var studentClasses = await _context.StudentClasses
                .Include(sc => sc.Student)
                    .ThenInclude(s => s!.Payments)
                        .ThenInclude(p => p.TuitionPeriod)
                .Include(sc => sc.Class)
                .Where(sc => sc.Class!.SemesterId == id)
                .OrderBy(sc => sc.Class!.Name)
                .ThenBy(sc => sc.Student!.Name)
                .ToListAsync();

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                studentClasses = studentClasses.Where(sc => sc.Class != null && sc.Class.SubjectId != null && subjectIds.Contains(sc.Class.SubjectId.Value)).ToList();
            }

            var allowedClassIds = studentClasses.Select(sc => sc.ClassId).Distinct().ToList();
            var allowedStudentIds = studentClasses.Select(sc => sc.StudentId).Distinct().ToList();
            var adjustments = await _context.TuitionAdjustments
                .Include(a => a.TuitionPeriod)
                .Where(a => a.TuitionPeriod!.SemesterId == id && allowedClassIds.Contains(a.ClassId) && allowedStudentIds.Contains(a.StudentId))
                .ToListAsync();
            var adjustmentDict = adjustments.ToDictionary(a => $"{a.StudentId}:{a.ClassId}:{a.TuitionPeriodId}");

            var studentRows = studentClasses.Select(sc =>
            {
                var startMonth = string.IsNullOrWhiteSpace(sc.StartMonth) ? sc.Student!.StartMonth : sc.StartMonth;
                var classPeriodIds = GetClassPeriodIds(periods, sc.Class!.StartDate, sc.Class!.EndDate);
                var payablePeriodIds = GetPayablePeriodIds(periods, sc.Class!.StartDate, sc.Class!.EndDate, startMonth);
                var rowAdjustments = payablePeriodIds
                    .Where(periodId => adjustmentDict.ContainsKey($"{sc.StudentId}:{sc.ClassId}:{periodId}"))
                    .Select(periodId =>
                    {
                        var adjustment = adjustmentDict[$"{sc.StudentId}:{sc.ClassId}:{periodId}"];
                        return new
                        {
                            PeriodId = periodId,
                            AdjustmentType = adjustment.AdjustmentType,
                            AdjustmentValue = adjustment.AdjustmentValue,
                            Note = adjustment.Note,
                            AmountDue = CalculateAdjustedTuition(sc.Class!.TuitionFee, adjustment.AdjustmentType, adjustment.AdjustmentValue)
                        };
                    })
                    .ToList();

                return new
                {
                    studentId = sc.StudentId,
                    studentName = sc.Student!.Name,
                    classId = sc.ClassId,
                    className = sc.Class!.Name,
                    classTuitionFee = sc.Class!.TuitionFee,
                    startMonth,
                    classPeriodIds,
                    payablePeriodIds,
                    amountDueByPeriod = payablePeriodIds.ToDictionary(
                        periodId => periodId.ToString(),
                        periodId =>
                        {
                            var adjustment = adjustmentDict.TryGetValue($"{sc.StudentId}:{sc.ClassId}:{periodId}", out var value) ? value : null;
                            return adjustment == null
                                ? sc.Class!.TuitionFee
                                : CalculateAdjustedTuition(sc.Class!.TuitionFee, adjustment.AdjustmentType, adjustment.AdjustmentValue);
                        }),
                    adjustments = rowAdjustments.ToDictionary(
                        adjustment => adjustment.PeriodId.ToString(),
                        adjustment => new
                        {
                            adjustment.AdjustmentType,
                            adjustment.AdjustmentValue,
                            adjustment.Note,
                            adjustment.AmountDue
                        }),
                    payments = sc.Student!.Payments
                        .Where(p => p.ClassId == sc.ClassId && p.TuitionPeriod!.SemesterId == id)
                        .GroupBy(p => p.TuitionPeriodId)
                        .ToDictionary(
                            g => g.Key.ToString(),
                            g =>
                            {
                                var latestPayment = g.Last();
                                return new
                                {
                                    AmountPaid = latestPayment.AmountPaid,
                                    Notes = string.Join("; ", g.Select(p => p.Notes).Where(n => !string.IsNullOrEmpty(n))),
                                    PaidAt = latestPayment.PaidAt
                                };
                            }
                        )
                };
            }).ToList();

            return Ok(new
            {
                periods = periods,
                students = studentRows
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

        private async Task EnsureTuitionPeriodsForClassDateRanges(int semesterId)
        {
            var classes = await _context.Classes
                .Where(c => c.SemesterId == semesterId && c.StartDate != null && c.EndDate != null)
                .Select(c => new { c.StartDate, c.EndDate })
                .ToListAsync();

            if (!classes.Any()) return;

            var baseStart = classes
                .Select(c => new DateOnly(c.StartDate!.Value.Year, c.StartDate.Value.Month, 1))
                .Min();

            var monthStarts = new SortedSet<DateOnly>();
            foreach (var cls in classes)
            {
                var cursor = new DateOnly(cls.StartDate!.Value.Year, cls.StartDate.Value.Month, 1);
                var end = new DateOnly(cls.EndDate!.Value.Year, cls.EndDate.Value.Month, 1);

                while (cursor <= end)
                {
                    monthStarts.Add(cursor);
                    cursor = cursor.AddMonths(1);
                }
            }

            var existing = await _context.TuitionPeriods
                .Where(p => p.SemesterId == semesterId)
                .ToListAsync();

            foreach (var period in existing)
            {
                if (TryParseMonth(period.MonthName, out var legacyMonth) && !TryParsePeriodStart(period.MonthName, out _))
                {
                    var inferredDate = FirstMonthOnOrAfter(baseStart, legacyMonth);
                    period.MonthName = FormatPeriodName(inferredDate);
                    period.DisplayOrder = ToPeriodOrder(inferredDate);
                }
            }

            foreach (var monthStart in monthStarts)
            {
                var monthName = FormatPeriodName(monthStart);
                if (!existing.Any(p => p.MonthName == monthName))
                {
                    _context.TuitionPeriods.Add(new TuitionPeriod
                    {
                        SemesterId = semesterId,
                        MonthName = monthName,
                        DisplayOrder = ToPeriodOrder(monthStart)
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private static List<int> GetClassPeriodIds(IEnumerable<dynamic> periods, DateOnly? classStartDate, DateOnly? classEndDate)
        {
            if (classStartDate != null && classEndDate != null)
            {
                var start = new DateOnly(classStartDate.Value.Year, classStartDate.Value.Month, 1);
                var end = new DateOnly(classEndDate.Value.Year, classEndDate.Value.Month, 1);
                return GetPeriodIdsForDateRange(periods, start, end);
            }

            return periods
                .OrderBy(p => (int)p.DisplayOrder)
                .Select(p => (int)p.Id)
                .ToList();
        }

        private static List<int> GetPayablePeriodIds(IEnumerable<dynamic> periods, DateOnly? classStartDate, DateOnly? classEndDate, string studentStartMonth)
        {
            if (classStartDate != null && classEndDate != null)
            {
                var classStart = new DateOnly(classStartDate.Value.Year, classStartDate.Value.Month, 1);
                var classEnd = new DateOnly(classEndDate.Value.Year, classEndDate.Value.Month, 1);
                var effectiveStart = classStart;

                if (TryParseMonth(studentStartMonth, out var studentMonth))
                {
                    var studentStart = new DateOnly(classStart.Year, studentMonth, 1);
                    if (studentStart < classStart)
                    {
                        studentStart = studentStart.AddYears(1);
                    }

                    effectiveStart = studentStart > classStart ? studentStart : classStart;
                }

                return GetPeriodIdsForDateRange(periods, effectiveStart, classEnd);
            }

            var fallbackStudentStartMonth = TryParseMonth(studentStartMonth, out var parsedMonth) ? parsedMonth : 1;

            return periods
                .Where(p => TryParseMonth((string)p.MonthName, out var month) && month >= fallbackStudentStartMonth)
                .OrderBy(p => (int)p.DisplayOrder)
                .Select(p => (int)p.Id)
                .ToList();
        }

        private static List<int> GetPeriodIdsForDateRange(IEnumerable<dynamic> periods, DateOnly start, DateOnly end)
        {
            if (end < start) return new List<int>();

            var periodByDate = periods
                .Where(p => TryParsePeriodStart((string)p.MonthName, out _))
                .GroupBy(p =>
                {
                    TryParsePeriodStart((string)p.MonthName, out var periodStart);
                    return periodStart;
                })
                .ToDictionary(g => g.Key, g => (int)g.First().Id);

            var result = new List<int>();
            var cursor = start;
            while (cursor <= end)
            {
                if (periodByDate.TryGetValue(cursor, out var periodId))
                {
                    result.Add(periodId);
                }

                cursor = cursor.AddMonths(1);
            }

            return result;
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

        private static int ToPeriodOrder(DateOnly periodStart)
        {
            return periodStart.Year * 100 + periodStart.Month;
        }

        private static string FormatPeriodName(DateOnly periodStart)
        {
            return $"T{periodStart.Month}/{periodStart.Year}";
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
    }
}
