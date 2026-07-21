namespace QLStudy.API.Models
{
    public class StudentReward
    {
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int RewardOptionId { get; set; }
        public RewardOption? RewardOption { get; set; }
    }
}
