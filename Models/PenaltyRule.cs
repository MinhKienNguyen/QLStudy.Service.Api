using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class PenaltyRule : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public decimal DefaultAmount { get; set; }
        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public ICollection<StudentPenalty> StudentPenalties { get; set; } = new List<StudentPenalty>();
    }
}
