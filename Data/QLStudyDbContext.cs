using Microsoft.EntityFrameworkCore;
using QLStudy.API.Models;

namespace QLStudy.API.Data
{
    public class QLStudyDbContext : DbContext
    {
        public QLStudyDbContext(DbContextOptions<QLStudyDbContext> options) : base(options)
        {
        }

        public DbSet<Semester> Semesters => Set<Semester>();
        public DbSet<Class> Classes => Set<Class>();
        public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<StudentClass> StudentClasses => Set<StudentClass>();
        public DbSet<RewardOption> RewardOptions => Set<RewardOption>();
        public DbSet<StudentReward> StudentRewards => Set<StudentReward>();
        public DbSet<TuitionPeriod> TuitionPeriods => Set<TuitionPeriod>();
        public DbSet<TuitionPayment> TuitionPayments => Set<TuitionPayment>();
        public DbSet<TuitionAdjustment> TuitionAdjustments => Set<TuitionAdjustment>();
        public DbSet<Attendance> Attendances => Set<Attendance>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserSubject> UserSubjects => Set<UserSubject>();
        public DbSet<ScreenPermission> ScreenPermissions => Set<ScreenPermission>();
        public DbSet<PenaltyRule> PenaltyRules => Set<PenaltyRule>();
        public DbSet<StudentPenalty> StudentPenalties => Set<StudentPenalty>();
        public DbSet<StudentScore> StudentScores => Set<StudentScore>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Class-Semester relation
            modelBuilder.Entity<Class>()
                .HasOne(c => c.Semester)
                .WithMany(s => s.Classes)
                .HasForeignKey(c => c.SemesterId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ClassSchedule-Class relation
            modelBuilder.Entity<ClassSchedule>()
                .HasOne(cs => cs.Class)
                .WithMany(c => c.Schedules)
                .HasForeignKey(cs => cs.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure StudentClass composite key & relationships
            modelBuilder.Entity<StudentClass>()
                .HasKey(sc => new { sc.StudentId, sc.ClassId });

            modelBuilder.Entity<StudentClass>()
                .HasOne(sc => sc.Student)
                .WithMany(s => s.StudentClasses)
                .HasForeignKey(sc => sc.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentClass>()
                .HasOne(sc => sc.Class)
                .WithMany(c => c.StudentClasses)
                .HasForeignKey(sc => sc.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure StudentReward composite key & relationships
            modelBuilder.Entity<StudentReward>()
                .HasKey(sr => new { sr.StudentId, sr.RewardOptionId });

            modelBuilder.Entity<StudentReward>()
                .HasOne(sr => sr.Student)
                .WithMany(s => s.StudentRewards)
                .HasForeignKey(sr => sr.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentReward>()
                .HasOne(sr => sr.RewardOption)
                .WithMany(r => r.StudentRewards)
                .HasForeignKey(sr => sr.RewardOptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TuitionPeriod-Semester relation
            modelBuilder.Entity<TuitionPeriod>()
                .HasOne(tp => tp.Semester)
                .WithMany(s => s.TuitionPeriods)
                .HasForeignKey(tp => tp.SemesterId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TuitionPayment-Student relation
            modelBuilder.Entity<TuitionPayment>()
                .HasOne(tp => tp.Student)
                .WithMany(s => s.Payments)
                .HasForeignKey(tp => tp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TuitionPayment-Class relation
            modelBuilder.Entity<TuitionPayment>()
                .HasOne(tp => tp.Class)
                .WithMany()
                .HasForeignKey(tp => tp.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure TuitionPayment-TuitionPeriod relation
            modelBuilder.Entity<TuitionPayment>()
                .HasOne(tp => tp.TuitionPeriod)
                .WithMany()
                .HasForeignKey(tp => tp.TuitionPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TuitionAdjustment>()
                .HasIndex(ta => new { ta.StudentId, ta.ClassId, ta.TuitionPeriodId })
                .IsUnique();

            modelBuilder.Entity<TuitionAdjustment>()
                .HasOne(ta => ta.Student)
                .WithMany()
                .HasForeignKey(ta => ta.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TuitionAdjustment>()
                .HasOne(ta => ta.Class)
                .WithMany()
                .HasForeignKey(ta => ta.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TuitionAdjustment>()
                .HasOne(ta => ta.TuitionPeriod)
                .WithMany()
                .HasForeignKey(ta => ta.TuitionPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Attendance relations
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Class)
                .WithMany()
                .HasForeignKey(a => a.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure UserSubject composite key & relationships
            modelBuilder.Entity<UserSubject>()
                .HasKey(us => new { us.UserId, us.SubjectId });

            modelBuilder.Entity<UserSubject>()
                .HasOne(us => us.User)
                .WithMany(u => u.UserSubjects)
                .HasForeignKey(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSubject>()
                .HasOne(us => us.Subject)
                .WithMany()
                .HasForeignKey(us => us.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Class-Subject relation
            modelBuilder.Entity<Class>()
                .HasOne(c => c.Subject)
                .WithMany()
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Class-Teacher relation
            modelBuilder.Entity<Class>()
                .HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudentPenalty>()
                .HasOne(sp => sp.Student)
                .WithMany()
                .HasForeignKey(sp => sp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentPenalty>()
                .HasOne(sp => sp.Class)
                .WithMany()
                .HasForeignKey(sp => sp.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentPenalty>()
                .HasOne(sp => sp.PenaltyRule)
                .WithMany(pr => pr.StudentPenalties)
                .HasForeignKey(sp => sp.PenaltyRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentScore>()
                .HasOne(ss => ss.Student)
                .WithMany()
                .HasForeignKey(ss => ss.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StudentScore>()
                .HasOne(ss => ss.Class)
                .WithMany()
                .HasForeignKey(ss => ss.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
