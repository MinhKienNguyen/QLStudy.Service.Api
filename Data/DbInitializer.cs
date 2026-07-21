using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Models;

namespace QLStudy.API.Data
{
    public static class DbInitializer
    {
        public static void Initialize(QLStudyDbContext context)
        {
            context.Database.EnsureCreated();
            EnsurePenaltyTables(context);
            EnsureScoreTables(context);

            // 1. Seed Subjects
            var subjects = new[]
            {
                new Subject { Name = "Toán" },
                new Subject { Name = "Văn" },
                new Subject { Name = "Anh" },
                new Subject { Name = "Lý" },
                new Subject { Name = "Hóa" },
                new Subject { Name = "KHTN" }
            };

            foreach (var s in subjects)
            {
                if (!context.Subjects.Any(sub => sub.Name == s.Name))
                {
                    context.Subjects.Add(s);
                }
            }
            context.SaveChanges();

            // 2. Seed Default Admin User
            string adminEmail = "admin@qlstudy.com";
            if (!context.Users.Any(u => u.Email == adminEmail))
            {
                var adminUser = new User
                {
                    FullName = "Trưởng bộ phận",
                    Email = adminEmail,
                    PhoneNumber = "0123456789",
                    Role = "Manager",
                    Status = "Active"
                };

                var passwordHasher = new PasswordHasher<User>();
                adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "admin");

                context.Users.Add(adminUser);
                context.SaveChanges();
            }

            // 3. Seed Screen Permissions
            var screenKeys = new[]
            {
                "dashboard", "schedule", "tuition", "students", "classes", "attendance", "penalties", "reports", "subjects", "accounts"
            };

            // Manager permissions
            foreach (var key in screenKeys)
            {
                var existing = context.ScreenPermissions.FirstOrDefault(p => p.Role == "Manager" && p.ScreenKey == key);
                if (existing == null)
                {
                    context.ScreenPermissions.Add(new ScreenPermission
                    {
                        Role = "Manager",
                        ScreenKey = key,
                        IsAllowed = true
                    });
                }
            }

            // Teacher permissions
            var teacherAllowedScreens = new[]
            {
                "dashboard", "schedule", "students", "classes", "attendance", "penalties", "tuition", "reports"
            };

            foreach (var key in screenKeys)
            {
                var existing = context.ScreenPermissions.FirstOrDefault(p => p.Role == "Teacher" && p.ScreenKey == key);
                bool allowed = teacherAllowedScreens.Contains(key);
                if (existing == null)
                {
                    context.ScreenPermissions.Add(new ScreenPermission
                    {
                        Role = "Teacher",
                        ScreenKey = key,
                        IsAllowed = allowed
                    });
                }
                else if (existing.IsAllowed != allowed)
                {
                    // Update if they mismatch default
                    existing.IsAllowed = allowed;
                }
            }
            context.SaveChanges();

            // 4. Seed default penalty rules
            var penaltyRules = new[]
            {
                new PenaltyRule { Name = "Sử dụng điện thoại trong giờ học", DefaultAmount = 5, IsActive = true },
                new PenaltyRule { Name = "Nói chuyện riêng khi giảng bài", DefaultAmount = 5, IsActive = true },
                new PenaltyRule { Name = "Đi học trễ quá 10 phút", DefaultAmount = 5, IsActive = true },
                new PenaltyRule { Name = "Nghỉ học không xin phép", DefaultAmount = 20, IsActive = true },
                new PenaltyRule { Name = "Nghỉ có xin phép nhưng không học bù", DefaultAmount = 10, IsActive = true },
                new PenaltyRule { Name = "Không làm bài tập về nhà", DefaultAmount = 10, IsActive = true },
                new PenaltyRule { Name = "Bài kiểm tra dưới 7 điểm", DefaultAmount = 5, IsActive = true },
                new PenaltyRule { Name = "Bài kiểm tra dưới 6 điểm", DefaultAmount = 10, IsActive = true },
                new PenaltyRule { Name = "Không thuộc nội quy lớp học", DefaultAmount = 5, IsActive = true }
            };

            foreach (var rule in penaltyRules)
            {
                if (!context.PenaltyRules.Any(r => r.Name == rule.Name))
                {
                    context.PenaltyRules.Add(rule);
                }
            }
            context.SaveChanges();

            // 5. Map Existing Classes to Subjects and set Tuition Fee
            var dbSubjects = context.Subjects.ToList();
            var classes = context.Classes.ToList();

            foreach (var cls in classes)
            {
                bool changed = false;

                // Map Subject
                if (cls.SubjectId == null)
                {
                    string classNameLower = cls.Name.ToLower();
                    Subject? matchedSubject = null;

                    if (classNameLower.Contains("toán"))
                    {
                        matchedSubject = dbSubjects.FirstOrDefault(s => s.Name == "Toán");
                    }
                    else if (classNameLower.Contains("văn"))
                    {
                        matchedSubject = dbSubjects.FirstOrDefault(s => s.Name == "Văn");
                    }
                    else if (classNameLower.Contains("anh") || classNameLower.Contains("english"))
                    {
                        matchedSubject = dbSubjects.FirstOrDefault(s => s.Name == "Anh");
                    }
                    else if (classNameLower.Contains("lý") || classNameLower.Contains("vật lý"))
                    {
                        matchedSubject = dbSubjects.FirstOrDefault(s => s.Name == "Lý");
                    }
                    else if (classNameLower.Contains("hóa"))
                    {
                        matchedSubject = dbSubjects.FirstOrDefault(s => s.Name == "Hóa");
                    }
                    else if (classNameLower.Contains("khtn"))
                    {
                        matchedSubject = dbSubjects.FirstOrDefault(s => s.Name == "KHTN");
                    }

                    if (matchedSubject != null)
                    {
                        cls.SubjectId = matchedSubject.Id;
                        changed = true;
                    }
                }

                // Default Tuition Fee
                if (cls.TuitionFee == 0)
                {
                    // Find payments for this class to infer tuition fee
                    var payments = context.TuitionPayments.Where(p => p.ClassId == cls.Id && p.AmountPaid > 0).Select(p => p.AmountPaid).ToList();
                    if (payments.Any())
                    {
                        // Use the most common (mode) payment amount, or average if they vary
                        var modePayment = payments.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key;
                        cls.TuitionFee = modePayment;
                    }
                    else
                    {
                        cls.TuitionFee = 1200; // Default tuition fee (1,200k or 1.2M)
                    }
                    changed = true;
                }

                if (changed)
                {
                    context.Classes.Update(cls);
                }
            }

            if (classes.Any())
            {
                context.SaveChanges();
            }
        }

        private static void EnsurePenaltyTables(QLStudyDbContext context)
        {
            if (context.Database.IsNpgsql())
            {
                context.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""PenaltyRules"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""Name"" text NOT NULL,
    ""DefaultAmount"" numeric NOT NULL,
    ""IsActive"" boolean NOT NULL
);

CREATE TABLE IF NOT EXISTS ""StudentPenalties"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""StudentId"" integer NOT NULL,
    ""ClassId"" integer NOT NULL,
    ""PenaltyRuleId"" integer NOT NULL,
    ""Date"" timestamp with time zone NOT NULL,
    ""Amount"" numeric NOT NULL,
    ""Note"" text NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""FK_StudentPenalties_Students_StudentId"" FOREIGN KEY (""StudentId"") REFERENCES ""Students"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_StudentPenalties_Classes_ClassId"" FOREIGN KEY (""ClassId"") REFERENCES ""Classes"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_StudentPenalties_PenaltyRules_PenaltyRuleId"" FOREIGN KEY (""PenaltyRuleId"") REFERENCES ""PenaltyRules"" (""Id"") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ""IX_StudentPenalties_StudentId"" ON ""StudentPenalties"" (""StudentId"");
CREATE INDEX IF NOT EXISTS ""IX_StudentPenalties_ClassId"" ON ""StudentPenalties"" (""ClassId"");
CREATE INDEX IF NOT EXISTS ""IX_StudentPenalties_PenaltyRuleId"" ON ""StudentPenalties"" (""PenaltyRuleId"");
CREATE INDEX IF NOT EXISTS ""IX_StudentPenalties_Date"" ON ""StudentPenalties"" (""Date"");
");
            }
        }

        private static void EnsureScoreTables(QLStudyDbContext context)
        {
            if (context.Database.IsNpgsql())
            {
                context.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ""StudentScores"" (
    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    ""StudentId"" integer NOT NULL,
    ""ClassId"" integer NOT NULL,
    ""Date"" timestamp with time zone NOT NULL,
    ""TestName"" text NOT NULL,
    ""Score"" numeric NOT NULL,
    ""MaxScore"" numeric NOT NULL,
    ""Note"" text NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    CONSTRAINT ""FK_StudentScores_Students_StudentId"" FOREIGN KEY (""StudentId"") REFERENCES ""Students"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_StudentScores_Classes_ClassId"" FOREIGN KEY (""ClassId"") REFERENCES ""Classes"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_StudentScores_StudentId"" ON ""StudentScores"" (""StudentId"");
CREATE INDEX IF NOT EXISTS ""IX_StudentScores_ClassId"" ON ""StudentScores"" (""ClassId"");
CREATE INDEX IF NOT EXISTS ""IX_StudentScores_Date"" ON ""StudentScores"" (""Date"");
");
            }
        }
    }
}
