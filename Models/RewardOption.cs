using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class RewardOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        public ICollection<StudentReward> StudentRewards { get; set; } = new List<StudentReward>();
    }
}
