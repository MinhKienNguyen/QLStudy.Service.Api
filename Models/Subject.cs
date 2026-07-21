using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Subject : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        public Center? Center { get; set; }
    }
}
