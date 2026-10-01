using System;
using System.Text.Json.Serialization;

namespace QLStudy.Domain.Entities
{
    public class Announcement : ITenantScoped
    {
        public int Id { get; set; }
        public int CenterId { get; set; } = 1;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = "Center"; // Center, Class
        public int? ClassId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public Center? Center { get; set; }

        [JsonIgnore]
        public Class? Class { get; set; }
    }
}
