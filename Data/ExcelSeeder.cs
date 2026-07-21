using System.Data;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Models;

namespace QLStudy.API.Data
{
    public class ExcelSeeder
    {
        private readonly QLStudyDbContext _context;

        public ExcelSeeder(QLStudyDbContext context)
        {
            _context = context;
        }

        public void Seed(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Excel file not found at: {filePath}");
            }

            // Seed default reward options if they don't exist
            var defaultRewards = new[] { "Học sinh xuất sắc", "Chuyên cần", "Tiến bộ vượt bậc" };
            foreach (var rewardName in defaultRewards)
            {
                if (!_context.RewardOptions.Any(r => r.Name == rewardName))
                {
                    _context.RewardOptions.Add(new RewardOption { Name = rewardName });
                }
            }
            _context.SaveChanges();

            // Required for ExcelDataReader encoding support
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();

            foreach (DataTable table in result.Tables)
            {
                string sheetName = table.TableName;
                
                // Delete existing semester if it has the same name to prevent duplicates on re-import
                var existingSemester = _context.Semesters
                    .Include(s => s.Classes)
                    .Include(s => s.TuitionPeriods)
                    .FirstOrDefault(s => s.Name == sheetName);
                if (existingSemester != null)
                {
                    _context.Semesters.Remove(existingSemester);
                    _context.SaveChanges();
                }

                var semester = new Semester
                {
                    Name = sheetName,
                    IsActive = sheetName == "Sheet1" // Make the newer/larger sheet active by default
                };
                _context.Semesters.Add(semester);
                _context.SaveChanges();

                // 1. Parse Schedule Grid (Rows 0-6 in DataTable, representing Excel rows 1-7)
                var classDict = new Dictionary<string, Class>();
                
                // Row 0 has days of week. B1 is Col index 1, C1 is 2, etc.
                // Col 1 = T2, Col 2 = T3, Col 3 = T4, Col 4 = T5, Col 5 = T6, Col 6 = T7, Col 7 = CN
                var dayNames = new Dictionary<int, string>
                {
                    { 1, "T2" }, { 2, "T3" }, { 3, "T4" }, { 4, "T5" }, { 5, "T6" }, { 6, "T7" }, { 7, "CN" }
                };

                for (int rowIndex = 1; rowIndex < 7; rowIndex++) // Excel rows 2 to 7
                {
                    if (rowIndex >= table.Rows.Count) break;
                    var row = table.Rows[rowIndex];
                    string timeSlot = row[0]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(timeSlot)) continue;

                    for (int colIndex = 1; colIndex <= 7; colIndex++)
                    {
                        if (colIndex >= table.Columns.Count) break;
                        string className = row[colIndex]?.ToString()?.Trim() ?? "";
                        if (string.IsNullOrEmpty(className)) continue;

                        if (!dayNames.TryGetValue(colIndex, out string? dayOfWeek)) continue;

                        // Normalize combined classes or split by + if necessary,
                        // but for schedule we store the exact label.
                        if (!classDict.TryGetValue(className, out var cls))
                        {
                            cls = new Class
                            {
                                Name = className,
                                SemesterId = semester.Id
                            };
                            _context.Classes.Add(cls);
                            _context.SaveChanges();
                            classDict[className] = cls;
                        }

                        var schedule = new ClassSchedule
                        {
                            ClassId = cls.Id,
                            DayOfWeek = dayOfWeek,
                            TimeSlot = timeSlot
                        };
                        _context.ClassSchedules.Add(schedule);
                    }
                }
                _context.SaveChanges();

                // 2. Parse Tuition Header (Row 7, representing Excel Row 8)
                // We'll scan row 7 for all columns that represent months (e.g. T7, T8, T9, T10, T11, T12, T1, T2, T3, T4, T5)
                var tuitionPeriods = new Dictionary<int, TuitionPeriod>();
                var headerRow = table.Rows[7];

                int displayOrder = 1;
                for (int colIndex = 2; colIndex < table.Columns.Count; colIndex++) // Starts from Col C or D
                {
                    string headerVal = headerRow[colIndex]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(headerVal)) continue;

                    // Match month columns like T9, T10, T1, etc.
                    if (headerVal.StartsWith("T") && int.TryParse(headerVal.Substring(1), out _))
                    {
                        var period = new TuitionPeriod
                        {
                            SemesterId = semester.Id,
                            MonthName = headerVal,
                            DisplayOrder = displayOrder++
                        };
                        _context.TuitionPeriods.Add(period);
                        _context.SaveChanges();
                        tuitionPeriods[colIndex] = period;
                    }
                }

                // 3. Parse Students (Row 8 onwards, representing Excel Row 9 onwards)
                string currentClassName = "Chưa phân lớp";
                
                // Determine sheet structure
                // Sheet 1 (Trang_tính1) has Starting Month in Col C (index 2) and Month Columns from Col D (index 3) onwards
                // Sheet 2 (Sheet1) has no Starting Month column, and Month Columns from Col C (index 2) onwards
                bool isSheet1 = sheetName == "Trang_tính1";

                for (int rowIndex = 8; rowIndex < table.Rows.Count; rowIndex++)
                {
                    var row = table.Rows[rowIndex];
                    string studentName = row[1]?.ToString()?.Trim() ?? ""; // Col B (index 1) is student name
                    string classColVal = row[0]?.ToString()?.Trim() ?? "";  // Col A (index 0) is class name

                    if (!string.IsNullOrEmpty(classColVal))
                    {
                        // Check if it's a totals row
                        if (classColVal.StartsWith("Tổng") || classColVal.StartsWith("tổng"))
                        {
                            break; // Stop when reaching the summary total row
                        }
                        currentClassName = classColVal;
                    }

                    if (string.IsNullOrEmpty(studentName)) continue;

                    // Skip headers or summary row labels
                    string lowerName = studentName.ToLower();
                    if (lowerName == "tổng" || lowerName == "tổng cộng" || lowerName == "học sinh" || 
                        lowerName == "tên học sinh" || lowerName == "tên hs" || lowerName == "tên")
                    {
                        continue;
                    }

                    // Check if current class exists in Db, if not, create it
                    if (!classDict.TryGetValue(currentClassName, out var studentClass))
                    {
                        studentClass = new Class
                        {
                            Name = currentClassName,
                            SemesterId = semester.Id
                        };
                        _context.Classes.Add(studentClass);
                        _context.SaveChanges();
                        classDict[currentClassName] = studentClass;
                    }

                    // Extract Starting Month
                    string startMonth = "";
                    if (isSheet1)
                    {
                        startMonth = row[2]?.ToString()?.Trim() ?? ""; // Col C (index 2) is joined month
                    }
                    else
                    {
                        startMonth = "T7"; // Default for Sheet1
                    }

                    // Check if Student already exists in this semester to avoid duplicates
                    var student = _context.Students
                        .Include(s => s.StudentClasses)
                        .FirstOrDefault(s => s.Name == studentName && s.StudentClasses.Any(sc => sc.Class!.SemesterId == semester.Id));

                    if (student == null)
                    {
                        student = new Student
                        {
                            Name = studentName,
                            StartMonth = startMonth
                        };
                        _context.Students.Add(student);
                        _context.SaveChanges();
                    }

                    // Enroll student in class (StudentClass join entity)
                    var studentClassRel = _context.StudentClasses
                        .FirstOrDefault(sc => sc.StudentId == student.Id && sc.ClassId == studentClass.Id);
                    if (studentClassRel == null)
                    {
                        studentClassRel = new StudentClass
                        {
                            StudentId = student.Id,
                            ClassId = studentClass.Id
                        };
                        _context.StudentClasses.Add(studentClassRel);
                        _context.SaveChanges();
                    }

                    // Parse Tuition Payments for this student and this class
                    foreach (var kvp in tuitionPeriods)
                    {
                        int colIndex = kvp.Key;
                        var period = kvp.Value;

                        // Ensure we do not read outside columns
                        if (colIndex >= table.Columns.Count) continue;

                        string cellVal = row[colIndex]?.ToString()?.Trim() ?? "";
                        decimal amountPaid = ParsePaymentAmount(cellVal);

                        if (amountPaid > 0)
                        {
                            var payment = new TuitionPayment
                            {
                                StudentId = student.Id,
                                ClassId = studentClass.Id, // Link payment to class
                                TuitionPeriodId = period.Id,
                                AmountPaid = amountPaid,
                                Notes = cellVal.Contains('+') ? $"Nhập tay: {cellVal}" : ""
                            };
                            _context.TuitionPayments.Add(payment);
                        }
                    }
                }
                _context.SaveChanges();
            }
        }

        private decimal ParsePaymentAmount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            
            // Remove spaces
            text = text.Replace(" ", "");

            // Handle addition formula in cell (e.g. 1300+1200 or 375+250)
            if (text.Contains('+'))
            {
                var parts = text.Split('+');
                decimal sum = 0;
                foreach (var part in parts)
                {
                    if (decimal.TryParse(part, out decimal val))
                    {
                        sum += val;
                    }
                }
                return sum;
            }

            if (decimal.TryParse(text, out decimal valSingle))
            {
                return valSingle;
            }

            return 0;
        }
    }
}
