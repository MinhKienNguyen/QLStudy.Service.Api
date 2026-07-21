using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Semester : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public ICollection<Class> Classes { get; set; } = new List<Class>();

        [JsonIgnore]
        public ICollection<TuitionPeriod> TuitionPeriods { get; set; } = new List<TuitionPeriod>();
    }
}
