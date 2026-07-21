using System;
using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Attendance : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public int ClassId { get; set; }
        public int StudentId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "Present"; // Present, Absent, Late
        public string Note { get; set; } = string.Empty;

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }
    }
}
