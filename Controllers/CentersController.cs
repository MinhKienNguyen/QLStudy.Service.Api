using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.Domain.Entities;
using QLStudy.Infrastructure.Data;
using System.Threading.Tasks;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CentersController : ControllerBase
    {
        private readonly QLStudyDbContext _context;

        public CentersController(QLStudyDbContext context)
        {
            _context = context;
        }

        // GET: api/centers/payment-settings
        [HttpGet("payment-settings")]
        public async Task<IActionResult> GetPaymentSettings()
        {
            var center = await _context.Centers.FirstOrDefaultAsync(c => c.Id == 1);
            if (center == null) return NotFound();

            return Ok(new
            {
                bankName = center.BankName,
                bankAccountNumber = center.BankAccountNumber,
                bankAccountName = center.BankAccountName,
                paymentQrCode = center.PaymentQrCode,
                payOSClientId = center.PayOSClientId,
                payOSApiKey = center.PayOSApiKey,
                payOSChecksumKey = center.PayOSChecksumKey
            });
        }

        // POST: api/centers/payment-settings
        [HttpPost("payment-settings")]
        public async Task<IActionResult> SavePaymentSettings([FromBody] PaymentSettingsModel model)
        {
            var center = await _context.Centers.FirstOrDefaultAsync(c => c.Id == 1);
            if (center == null) return NotFound();

            center.BankName = model.BankName;
            center.BankAccountNumber = model.BankAccountNumber;
            center.BankAccountName = model.BankAccountName;
            center.PaymentQrCode = model.PaymentQrCode;
            center.PayOSClientId = model.PayOSClientId;
            center.PayOSApiKey = model.PayOSApiKey;
            center.PayOSChecksumKey = model.PayOSChecksumKey;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }

    public class PaymentSettingsModel
    {
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public string? PaymentQrCode { get; set; }
        public string? PayOSClientId { get; set; }
        public string? PayOSApiKey { get; set; }
        public string? PayOSChecksumKey { get; set; }
    }
}
