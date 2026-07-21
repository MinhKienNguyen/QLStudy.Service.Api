namespace QLStudy.API.Models
{
    public class StudentPenalty
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int ClassId { get; set; }
        public int PenaltyRuleId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Student? Student { get; set; }
        public Class? Class { get; set; }
        public PenaltyRule? PenaltyRule { get; set; }
    }
}
