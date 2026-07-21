namespace QLStudy.API.Models
{
    public class StudentClass
    {
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public int ClassId { get; set; }
        public Class? Class { get; set; }

        public string StartMonth { get; set; } = string.Empty;
    }
}
