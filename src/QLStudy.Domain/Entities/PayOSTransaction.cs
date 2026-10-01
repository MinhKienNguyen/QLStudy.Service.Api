using System;
using System.Text.Json.Serialization;

namespace QLStudy.Domain.Entities
{
    public class PayOSTransaction : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public int StudentId { get; set; }
        public int? ClassId { get; set; }
        public int TuitionPeriodId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Paid, Cancelled
        public string? PaymentLinkId { get; set; }
        public string? CheckoutUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }

        [JsonIgnore]
        public TuitionPeriod? TuitionPeriod { get; set; }
    }
}
