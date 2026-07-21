using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class User : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Teacher"; // Manager, Teacher
        public string Status { get; set; } = "Active"; // Active, Locked
        public string? Token { get; set; }

        [JsonIgnore]
        public Center? Center { get; set; }

        public ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
    }
}
