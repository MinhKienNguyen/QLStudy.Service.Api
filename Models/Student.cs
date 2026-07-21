using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Student : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string StartMonth { get; set; } = string.Empty; // e.g., T4, T2

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public ICollection<StudentClass> StudentClasses { get; set; } = new List<StudentClass>();

        [JsonIgnore]
        public ICollection<TuitionPayment> Payments { get; set; } = new List<TuitionPayment>();

        [JsonIgnore]
        public ICollection<StudentReward> StudentRewards { get; set; } = new List<StudentReward>();
    }
}
