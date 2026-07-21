using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class TuitionPeriod
    {
        public int Id { get; set; }
        public int SemesterId { get; set; }
        public string MonthName { get; set; } = string.Empty; // e.g., T9, T10
        public int DisplayOrder { get; set; }

        [JsonIgnore]
        public Semester? Semester { get; set; }
    }
}
