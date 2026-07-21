using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TuitionController : BaseApiController
    {
        public TuitionController(QLStudyDbContext context) : base(context)
        {
        }

        public record PaymentUpdateDto(int StudentId, int ClassId, int PeriodId, decimal Amount, string Notes);
        public record TuitionAdjustmentDto(int StudentId, int ClassId, int PeriodId, string AdjustmentType, decimal AdjustmentValue, string Note);

        // POST: api/tuition/payment
        [HttpPost("payment")]
        public async Task<IActionResult> SavePayment([FromBody] PaymentUpdateDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            // Verify teacher subject scope
            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                var targetClass = await _context.Classes.FindAsync(dto.ClassId);
                if (targetClass == null || targetClass.SubjectId == null || !subjectIds.Contains(targetClass.SubjectId.Value))
                {
                    return Forbid("Bạn không có quyền cập nhật học phí của lớp học này.");
                }
            }

            var studentExists = await _context.Students.AnyAsync(s => s.Id == dto.StudentId);
            if (!studentExists) return BadRequest("Student not found.");

            var classExists = await _context.Classes.AnyAsync(c => c.Id == dto.ClassId);
            if (!classExists) return BadRequest("Class not found.");

            var periodExists = await _context.TuitionPeriods.AnyAsync(p => p.Id == dto.PeriodId);
            if (!periodExists) return BadRequest("Tuition period not found.");

            var existingPayment = await _context.TuitionPayments
                .FirstOrDefaultAsync(p => p.StudentId == dto.StudentId && p.ClassId == dto.ClassId && p.TuitionPeriodId == dto.PeriodId);

            if (existingPayment != null)
            {
                if (dto.Amount <= 0)
                {
                    // If amount is set to 0 or negative, remove the payment record
                    _context.TuitionPayments.Remove(existingPayment);
                }
                else
                {
                    // Update existing
                    existingPayment.AmountPaid = dto.Amount;
                    existingPayment.Notes = dto.Notes;
                    existingPayment.PaidAt = DateTime.UtcNow;
                    _context.TuitionPayments.Update(existingPayment);
                }
            }
            else if (dto.Amount > 0)
            {
                // Create new payment record
                var newPayment = new TuitionPayment
                {
                    StudentId = dto.StudentId,
                    ClassId = dto.ClassId,
                    TuitionPeriodId = dto.PeriodId,
                    AmountPaid = dto.Amount,
                    Notes = dto.Notes,
                    PaidAt = DateTime.UtcNow
                };
                await _context.TuitionPayments.AddAsync(newPayment);
            }

            await _context.SaveChangesAsync();
            var savedPayment = await _context.TuitionPayments
                .Where(p => p.StudentId == dto.StudentId && p.ClassId == dto.ClassId && p.TuitionPeriodId == dto.PeriodId)
                .Select(p => new { p.AmountPaid, p.Notes, p.PaidAt })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                message = "Payment updated successfully",
                amountPaid = savedPayment?.AmountPaid ?? 0,
                notes = savedPayment?.Notes ?? string.Empty,
                paidAt = savedPayment?.PaidAt
            });
        }

        // POST: api/tuition/adjustment
        [HttpPost("adjustment")]
        public async Task<IActionResult> SaveAdjustment([FromBody] TuitionAdjustmentDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var targetClass = await _context.Classes.FindAsync(dto.ClassId);
            if (targetClass == null) return BadRequest("Class not found.");

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                if (targetClass.SubjectId == null || !subjectIds.Contains(targetClass.SubjectId.Value))
                {
                    return Forbid("Báº¡n khÃ´ng cÃ³ quyá»n cáº­p nháº­t há»c phÃ­ cá»§a lá»›p há»c nÃ y.");
                }
            }

            var studentExists = await _context.Students.AnyAsync(s => s.Id == dto.StudentId);
            if (!studentExists) return BadRequest("Student not found.");

            var periodExists = await _context.TuitionPeriods.AnyAsync(p => p.Id == dto.PeriodId);
            if (!periodExists) return BadRequest("Tuition period not found.");

            var isEnrolled = await _context.StudentClasses.AnyAsync(sc => sc.StudentId == dto.StudentId && sc.ClassId == dto.ClassId);
            if (!isEnrolled) return BadRequest("Student is not enrolled in this class.");

            var adjustmentType = NormalizeAdjustmentType(dto.AdjustmentType);
            if (adjustmentType == null) return BadRequest("Invalid adjustment type.");

            var existingAdjustment = await _context.TuitionAdjustments
                .FirstOrDefaultAsync(a => a.StudentId == dto.StudentId && a.ClassId == dto.ClassId && a.TuitionPeriodId == dto.PeriodId);

            if (adjustmentType == "None")
            {
                if (existingAdjustment != null)
                {
                    _context.TuitionAdjustments.Remove(existingAdjustment);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    studentId = dto.StudentId,
                    classId = dto.ClassId,
                    periodId = dto.PeriodId,
                    adjustmentType = "None",
                    adjustmentValue = 0,
                    note = string.Empty,
                    amountDue = targetClass.TuitionFee
                });
            }

            var adjustmentValue = adjustmentType == "Free" ? 0 : Math.Max(0, dto.AdjustmentValue);
            var note = dto.Note?.Trim() ?? string.Empty;

            if (existingAdjustment == null)
            {
                existingAdjustment = new TuitionAdjustment
                {
                    StudentId = dto.StudentId,
                    ClassId = dto.ClassId,
                    TuitionPeriodId = dto.PeriodId,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.TuitionAdjustments.AddAsync(existingAdjustment);
            }

            existingAdjustment.AdjustmentType = adjustmentType;
            existingAdjustment.AdjustmentValue = adjustmentValue;
            existingAdjustment.Note = note;
            existingAdjustment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                studentId = dto.StudentId,
                classId = dto.ClassId,
                periodId = dto.PeriodId,
                adjustmentType = existingAdjustment.AdjustmentType,
                adjustmentValue = existingAdjustment.AdjustmentValue,
                note = existingAdjustment.Note,
                amountDue = CalculateAdjustedTuition(targetClass.TuitionFee, existingAdjustment.AdjustmentType, existingAdjustment.AdjustmentValue)
            });
        }

        // DELETE: api/tuition/adjustment?studentId=1&classId=2&periodId=3
        [HttpDelete("adjustment")]
        public async Task<IActionResult> DeleteAdjustment([FromQuery] int studentId, [FromQuery] int classId, [FromQuery] int periodId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var targetClass = await _context.Classes.FindAsync(classId);
            if (targetClass == null) return BadRequest("Class not found.");

            if (user.Role == "Teacher")
            {
                var subjectIds = await GetTeacherSubjectIdsAsync(user.Id);
                if (targetClass.SubjectId == null || !subjectIds.Contains(targetClass.SubjectId.Value))
                {
                    return Forbid("Báº¡n khÃ´ng cÃ³ quyá»n cáº­p nháº­t há»c phÃ­ cá»§a lá»›p há»c nÃ y.");
                }
            }

            var existingAdjustment = await _context.TuitionAdjustments
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.ClassId == classId && a.TuitionPeriodId == periodId);

            if (existingAdjustment != null)
            {
                _context.TuitionAdjustments.Remove(existingAdjustment);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                studentId,
                classId,
                periodId,
                adjustmentType = "None",
                adjustmentValue = 0,
                note = string.Empty,
                amountDue = targetClass.TuitionFee
            });
        }

        private static string? NormalizeAdjustmentType(string? type)
        {
            return type switch
            {
                "None" => "None",
                "DiscountPercent" => "DiscountPercent",
                "DiscountAmount" => "DiscountAmount",
                "FixedAmount" => "FixedAmount",
                "Free" => "Free",
                _ => null
            };
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
