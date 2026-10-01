using System.Text.Json.Serialization;

namespace QLStudy.Domain.Entities
{
    public class StudentClassEnrollment : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public string StartMonth { get; set; } = string.Empty;
        public string? EndMonth { get; set; }
        public string Status { get; set; } = "Active";
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }
    }
}
