using System;
using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Attendance
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public int StudentId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "Present"; // Present, Absent, Late
        public string Note { get; set; } = string.Empty;

        [JsonIgnore]
        public Class? Class { get; set; }

        [JsonIgnore]
        public Student? Student { get; set; }
    }
}
