using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class ClassSchedule
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public string DayOfWeek { get; set; } = string.Empty; // T2, T3, T4, T5, T6, T7, CN
        public string TimeSlot { get; set; } = string.Empty; // e.g., 7h30-9h

        [JsonIgnore]
        public Class? Class { get; set; }
    }
}
