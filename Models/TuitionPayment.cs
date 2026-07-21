using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class TuitionPayment
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int TuitionPeriodId { get; set; }
        public decimal AmountPaid { get; set; } // stored in thousands, e.g. 500 = 500,000 VND
        public string Notes { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }
        public Class? Class { get; set; }
        public TuitionPeriod? TuitionPeriod { get; set; }
    }
}
