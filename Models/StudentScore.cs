using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class StudentScore : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public DateTime Date { get; set; }
        public string TestName { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; } = 10;
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }
    }
}
