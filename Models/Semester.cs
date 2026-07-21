using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Semester
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        [JsonIgnore]
        public ICollection<Class> Classes { get; set; } = new List<Class>();

        [JsonIgnore]
        public ICollection<TuitionPeriod> TuitionPeriods { get; set; } = new List<TuitionPeriod>();
    }
}
