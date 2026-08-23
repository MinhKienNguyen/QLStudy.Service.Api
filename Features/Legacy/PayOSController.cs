using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Domain.Entities;
using QLStudy.Infrastructure.Data;
using PayOS;
using PayOS.Models.Webhooks;
using PayOS.Models.V2.PaymentRequests;

namespace QLStudy.Service.Api.Features.Legacy
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayOSController : BaseApiController
    {
        public PayOSController(QLStudyDbContext context) : base(context)
        {
        }

        public record CreatePaymentLinkDto(int StudentId, int? ClassId, int PeriodId, decimal Amount);

        // POST: api/payos/create-payment-link
        [HttpPost("create-payment-link")]
        public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePaymentLinkDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var student = await _context.Students.FindAsync(dto.StudentId);
            if (student == null) return NotFound("Student not found.");

            var center = await _context.Centers.FindAsync(student.CenterId);
            if (center == null) return NotFound("Center not found.");

            // Verify if PayOS is configured
            if (string.IsNullOrEmpty(center.PayOSClientId) || 
                string.IsNullOrEmpty(center.PayOSApiKey) || 
                string.IsNullOrEmpty(center.PayOSChecksumKey))
            {
                return BadRequest("Cổng thanh toán trực tuyến PayOS chưa được cấu hình cho trung tâm này.");
            }

            // Verify tuition period
            var period = await _context.TuitionPeriods.FindAsync(dto.PeriodId);
            if (period == null) return NotFound("Tuition period not found.");

            // 1. Create a Pending transaction in our database
            var transaction = new PayOSTransaction
            {
                CenterId = student.CenterId,
                StudentId = dto.StudentId,
                ClassId = dto.ClassId,
                TuitionPeriodId = dto.PeriodId,
                Amount = dto.Amount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _context.PayOSTransactions.AddAsync(transaction);
            await _context.SaveChangesAsync(); // Saves to database to get transaction.Id

            try
            {
                // 2. Instantiate PayOS Client
                var payOS = new PayOSClient(center.PayOSClientId, center.PayOSApiKey, center.PayOSChecksumKey);

                // 3. Setup redirect URLs
                string baseUrl = "http://localhost:4301";
                if (Request.Headers.TryGetValue("Origin", out var origin))
                {
                    baseUrl = origin.ToString();
                }

                string returnUrl = $"{baseUrl}/dashboard?paymentStatus=success&transactionId={transaction.Id}";
                string cancelUrl = $"{baseUrl}/dashboard?paymentStatus=cancel&transactionId={transaction.Id}";

                // 4. Create PayOS Payment Request
                // Note: description must be strictly <= 9 chars for non-linked banks
                string description = $"HP {transaction.Id}";
                int amountVnd = Convert.ToInt32(dto.Amount);

                var paymentRequest = new CreatePaymentLinkRequest
                {
                    OrderCode = transaction.Id,
                    Amount = amountVnd,
                    Description = description,
                    CancelUrl = cancelUrl,
                    ReturnUrl = returnUrl,
                    Items = new List<PaymentLinkItem>
                    {
                        new PaymentLinkItem
                        {
                            Name = "Hoc phi QLStudy",
                            Quantity = 1,
                            Price = amountVnd
                        }
                    }
                };

                // 5. Call PayOS API
                var response = await payOS.PaymentRequests.CreateAsync(paymentRequest);

                // 6. Update transaction in database with links
                transaction.PaymentLinkId = response.PaymentLinkId;
                transaction.CheckoutUrl = response.CheckoutUrl;
                _context.PayOSTransactions.Update(transaction);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    checkoutUrl = response.CheckoutUrl,
                    orderCode = response.OrderCode,
                    paymentLinkId = response.PaymentLinkId
                });
            }
            catch (Exception ex)
            {
                // Fail transaction and save error if PayOS API fails
                transaction.Status = "Cancelled";
                _context.PayOSTransactions.Update(transaction);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Error creating PayOS payment link: {ex.Message}");
                return BadRequest($"Lỗi kết nối cổng thanh toán PayOS: {ex.Message}");
            }
        }

        // POST: api/payos/webhook
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook([FromBody] Webhook webhookBody)
        {
            // Webhook might be called without active tenant scope, so we extract center from transaction later
            // PayOS first setup might send a test webhook with orderCode = 123
            if (webhookBody.Data != null && webhookBody.Data.OrderCode == 123)
            {
                return Ok(new { success = true, message = "Test webhook verified." });
            }

            try
            {
                if (webhookBody.Data == null)
                {
                    return BadRequest("Webhook payload data is null.");
                }

                int transactionId = (int)webhookBody.Data.OrderCode;
                var transaction = await _context.PayOSTransactions.FindAsync(transactionId);
                if (transaction == null) return NotFound("Transaction not found.");

                var center = await _context.Centers.FindAsync(transaction.CenterId);
                if (center == null) return NotFound("Center not found.");

                // Instantiate client to verify signature
                var payOS = new PayOSClient(center.PayOSClientId, center.PayOSApiKey, center.PayOSChecksumKey);
                var verifiedData = await payOS.Webhooks.VerifyAsync(webhookBody);

                if (verifiedData == null) return BadRequest("Signature verification failed.");

                // If paid successfully, process tuition record
                if (transaction.Status == "Pending")
                {
                    transaction.Status = "Paid";
                    transaction.PaidAt = DateTime.UtcNow;

                    // Tuition values in database are in thousands (e.g. 500 = 500,000 VND)
                    decimal amountInThousands = transaction.Amount / 1000;

                    if (transaction.ClassId.HasValue)
                    {
                        // Apply directly to specified class
                        var existingPayment = await _context.TuitionPayments
                            .FirstOrDefaultAsync(p => p.StudentId == transaction.StudentId && 
                                                     p.ClassId == transaction.ClassId.Value && 
                                                     p.TuitionPeriodId == transaction.TuitionPeriodId);

                        if (existingPayment != null)
                        {
                            existingPayment.AmountPaid += amountInThousands;
                            existingPayment.Notes = string.IsNullOrEmpty(existingPayment.Notes) 
                                ? $"Thanh toán PayOS: +{amountInThousands}k (GD: {transaction.Id})" 
                                : existingPayment.Notes + $", +{amountInThousands}k (GD: {transaction.Id})";
                            existingPayment.PaidAt = DateTime.UtcNow;
                            _context.TuitionPayments.Update(existingPayment);
                        }
                        else
                        {
                            var newPayment = new TuitionPayment
                            {
                                CenterId = transaction.CenterId,
                                StudentId = transaction.StudentId,
                                ClassId = transaction.ClassId.Value,
                                TuitionPeriodId = transaction.TuitionPeriodId,
                                AmountPaid = amountInThousands,
                                Notes = $"Thanh toán PayOS (GD: {transaction.Id})",
                                PaidAt = DateTime.UtcNow
                            };
                            await _context.TuitionPayments.AddAsync(newPayment);
                        }
                    }
                    else
                    {
                        // Distribute to all unpaid classes for this student in this period
                        var studentClasses = await _context.StudentClasses
                            .Include(sc => sc.Class)
                            .Where(sc => sc.StudentId == transaction.StudentId)
                            .ToListAsync();

                        var payments = await _context.TuitionPayments
                            .Where(p => p.StudentId == transaction.StudentId && p.TuitionPeriodId == transaction.TuitionPeriodId)
                            .ToListAsync();
                        var paymentDict = payments.ToDictionary(p => p.ClassId);

                        var adjustments = await _context.TuitionAdjustments
                            .Where(a => a.StudentId == transaction.StudentId && a.TuitionPeriodId == transaction.TuitionPeriodId)
                            .ToListAsync();
                        var adjustmentDict = adjustments.ToDictionary(a => a.ClassId);

                        decimal remainingAmount = amountInThousands;

                        foreach (var sc in studentClasses)
                        {
                            if (remainingAmount <= 0) break;

                            decimal standardFee = sc.Class!.TuitionFee;
                            decimal adjustedFee = standardFee;
                            if (adjustmentDict.TryGetValue(sc.ClassId, out var adj))
                            {
                                adjustedFee = adj.AdjustmentType switch
                                {
                                    "DiscountPercent" => standardFee * (100 - Math.Min(100, Math.Max(0, adj.AdjustmentValue))) / 100,
                                    "DiscountAmount" => standardFee - Math.Max(0, adj.AdjustmentValue),
                                    "FixedAmount" => Math.Max(0, adj.AdjustmentValue),
                                    "Free" => 0,
                                    _ => standardFee
                                };
                            }

                            decimal currentPaid = 0;
                            if (paymentDict.TryGetValue(sc.ClassId, out var payment))
                            {
                                currentPaid = payment.AmountPaid;
                            }

                            decimal unpaid = adjustedFee - currentPaid;
                            if (unpaid > 0)
                            {
                                decimal applyAmount = Math.Min(remainingAmount, unpaid);
                                remainingAmount -= applyAmount;

                                if (payment != null)
                                {
                                    payment.AmountPaid += applyAmount;
                                    payment.Notes = string.IsNullOrEmpty(payment.Notes)
                                        ? $"Thanh toán PayOS (GD gộp: {transaction.Id})"
                                        : payment.Notes + $", +{applyAmount}k (GD gộp: {transaction.Id})";
                                    payment.PaidAt = DateTime.UtcNow;
                                    _context.TuitionPayments.Update(payment);
                                }
                                else
                                {
                                    var newPayment = new TuitionPayment
                                    {
                                        CenterId = transaction.CenterId,
                                        StudentId = transaction.StudentId,
                                        ClassId = sc.ClassId,
                                        TuitionPeriodId = transaction.TuitionPeriodId,
                                        AmountPaid = applyAmount,
                                        Notes = $"Thanh toán PayOS (GD gộp: {transaction.Id})",
                                        PaidAt = DateTime.UtcNow
                                    };
                                    await _context.TuitionPayments.AddAsync(newPayment);
                                }
                            }
                        }

                        // Apply residual surplus amount to the first class if any exists
                        if (remainingAmount > 0 && studentClasses.Count > 0)
                        {
                            var firstClassId = studentClasses[0].ClassId;
                            if (paymentDict.TryGetValue(firstClassId, out var payment))
                            {
                                payment.AmountPaid += remainingAmount;
                                payment.Notes += $" (Thừa +{remainingAmount}k GD: {transaction.Id})";
                                payment.PaidAt = DateTime.UtcNow;
                                _context.PayOSTransactions.Update(transaction);
                            }
                            else
                            {
                                var newPayment = new TuitionPayment
                                {
                                    CenterId = transaction.CenterId,
                                    StudentId = transaction.StudentId,
                                    ClassId = firstClassId,
                                    TuitionPeriodId = transaction.TuitionPeriodId,
                                    AmountPaid = remainingAmount,
                                    Notes = $"Thanh toán PayOS (Thừa +{remainingAmount}k GD: {transaction.Id})",
                                    PaidAt = DateTime.UtcNow
                                };
                                await _context.TuitionPayments.AddAsync(newPayment);
                            }
                        }
                    }

                    _context.PayOSTransactions.Update(transaction);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Webhook processing error: {ex.Message}");
                return BadRequest($"Webhook error: {ex.Message}");
            }
        }
    }
}
