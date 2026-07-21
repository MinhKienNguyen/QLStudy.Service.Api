using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class TuitionAdjustment
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int TuitionPeriodId { get; set; }
        public string AdjustmentType { get; set; } = "None";
        public decimal AdjustmentValue { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Student? Student { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }

        [JsonIgnore]
        public TuitionPeriod? TuitionPeriod { get; set; }
    }
}
