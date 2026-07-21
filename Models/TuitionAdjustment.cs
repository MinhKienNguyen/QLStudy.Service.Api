using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class TuitionAdjustment : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int TuitionPeriodId { get; set; }
        public string AdjustmentType { get; set; } = "None";
        public decimal AdjustmentValue { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

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
