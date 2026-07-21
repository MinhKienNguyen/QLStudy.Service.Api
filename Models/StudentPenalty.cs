using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class StudentPenalty : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int PenaltyRuleId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }

        [JsonIgnore]
        public PenaltyRule? PenaltyRule { get; set; }
    }
}
