using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class UserSubject
    {
        public int UserId { get; set; }
        public int SubjectId { get; set; }

        [JsonIgnore]
        public User? User { get; set; }
        public Subject? Subject { get; set; }
    }
}
