using System.Text.Json.Serialization;

namespace QLStudy.API.Models
{
    public class Class
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SemesterId { get; set; }
        public int? SubjectId { get; set; }
        public int? TeacherId { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public decimal TuitionFee { get; set; } = 0;

        public Semester? Semester { get; set; }
        public Subject? Subject { get; set; }
        public User? Teacher { get; set; }
        public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();

        [JsonIgnore]
        public ICollection<StudentClass> StudentClasses { get; set; } = new List<StudentClass>();
    }
}
