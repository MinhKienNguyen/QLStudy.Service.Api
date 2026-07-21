namespace QLStudy.API.Models
{
    public class ScreenPermission
    {
        public int Id { get; set; }
        public string Role { get; set; } = string.Empty; // Manager, Teacher
        public string ScreenKey { get; set; } = string.Empty; // dashboard, schedule, tuition, students, classes, attendance, reports, subjects, accounts
        public bool IsAllowed { get; set; }
    }
}
