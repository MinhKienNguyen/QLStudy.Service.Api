using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class PenaltyRule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal DefaultAmount { get; set; }
        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public ICollection<StudentPenalty> StudentPenalties { get; set; } = new List<StudentPenalty>();
    }
}
