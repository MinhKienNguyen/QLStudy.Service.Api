using Microsoft.EntityFrameworkCore;
using QLStudy.Application.Common.Tenancy;
using QLStudy.Domain.Entities;
using QLStudy.Infrastructure.Data;
using QLStudy.Service.Api.Middleware;
using Serilog;
using System.Diagnostics;
using TLog;
using TLog.Extensions;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.AddTLog(builder.Configuration, _ => { });

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// Configure EF Core with SQLite or PostgreSQL dynamically
var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
builder.Services.AddDbContext<QLStudyDbContext>(options =>
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection"));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection"));
    }
});

// Register Excel Seeder
builder.Services.AddTransient<ExcelSeeder>();

// Configure CORS for Angular Frontend (Allow credentials for HttpOnly Cookie)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost:4300")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();
app.Services.GetService<ILogWriter>()?.Information("QLStudy backend started", app.Environment.EnvironmentName, "Program");
app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngular");
app.UseMiddleware<TenantMiddleware>();

app.Use(async (context, next) =>
{
    var logWriter = context.RequestServices.GetService<ILogWriter>();
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await next();
        stopwatch.Stop();

        logWriter?.Information(
            $"HTTP {context.Request.Method} {context.Request.Path} responded {context.Response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms",
            string.Empty,
            "HttpRequest");
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        logWriter?.Exception(
            ex,
            $"HTTP {context.Request.Method} {context.Request.Path} failed after {stopwatch.ElapsedMilliseconds}ms",
            "HttpRequest");
        throw;
    }
});

app.UseAuthorization();

app.MapControllers();

// Apply migrations and seed database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<QLStudyDbContext>();

        if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            context.Database.EnsureCreated();
        }
        else
        {
            context.Database.Migrate();
        }

        EnsureTuitionPaymentPaidAtColumn(context, provider);
        EnsureTuitionAdjustmentsTable(context, provider);
        EnsureStudentClassEnrollmentsTable(context, provider);
        EnsureAnnouncementsTable(context, provider);
        EnsurePayOSTransactionsTable(context, provider);
        EnsureTenantSchema(context, provider);
        EnsureCenterPaymentColumns(context, provider);

        // Seed default subjects, admin account, screen permissions, and update existing classes
        DbInitializer.Initialize(context);
        EnsureTenantSchema(context, provider);
        Console.WriteLine("RBAC and Subject config initialized successfully!");

        var seedExcelOnStartup = builder.Configuration.GetValue<bool>("SeedExcelOnStartup");

        // Seed data if explicitly enabled and database is empty
        if (seedExcelOnStartup && !context.Semesters.Any())
        {
            var seeder = services.GetRequiredService<ExcelSeeder>();
            string excelPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "lá»‹ch dáº¡y.xlsx"));
            seeder.Seed(excelPath);
            // Re-run initializer to map the newly imported classes to subjects
            DbInitializer.Initialize(context);
            Console.WriteLine("Database auto-seeded from lá»‹ch dáº¡y.xlsx successfully!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred during database migration or seeding: {ex.Message}");
    }
}

app.Run();

static void EnsureTenantSchema(QLStudyDbContext context, string provider)
{
    var tenantTables = new[]
    {
        "Users",
        "Semesters",
        "Classes",
        "ClassSchedules",
        "Students",
        "RewardOptions",
        "TuitionPeriods",
        "TuitionPayments",
        "TuitionAdjustments",
        "Attendances",
        "Subjects",
        "PenaltyRules",
        "StudentPenalties",
        "StudentScores",
        "Announcements",
        "PayOSTransactions"
    };

    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Centers" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "Code" text NOT NULL,
                "Name" text NOT NULL,
                "LogoUrl" text NULL,
                "Domain" text NULL,
                "Status" text NOT NULL DEFAULT 'Active',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Centers_Code" ON "Centers" ("Code");

            INSERT INTO "Centers" ("Id", "Code", "Name", "Status", "CreatedAt")
            VALUES (1, 'default', 'Trung tÃ¢m máº·c Ä‘á»‹nh', 'Active', NOW())
            ON CONFLICT ("Id") DO NOTHING;
            """);

        foreach (var table in tenantTables)
        {
            context.Database.ExecuteSqlRaw($"""
                ALTER TABLE "{table}"
                ADD COLUMN IF NOT EXISTS "CenterId" integer NOT NULL DEFAULT 1;

                CREATE INDEX IF NOT EXISTS "IX_{table}_CenterId" ON "{table}" ("CenterId");
                """);
        }
    }
    else
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Centers" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Centers" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "LogoUrl" TEXT NULL,
                "Domain" TEXT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Active',
                "CreatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Centers_Code" ON "Centers" ("Code");

            INSERT OR IGNORE INTO "Centers" ("Id", "Code", "Name", "Status", "CreatedAt")
            VALUES (1, 'default', 'Trung tÃ¢m máº·c Ä‘á»‹nh', 'Active', CURRENT_TIMESTAMP);
            """);

        foreach (var table in tenantTables)
        {
            var existingColumns = context.Database
                .SqlQueryRaw<string>($"""SELECT name AS "Value" FROM pragma_table_info('{table}')""")
                .ToList();

            if (!existingColumns.Contains("CenterId"))
            {
                context.Database.ExecuteSqlRaw($"""ALTER TABLE "{table}" ADD COLUMN "CenterId" INTEGER NOT NULL DEFAULT 1;""");
            }
        }
    }
}

static void EnsureTuitionPaymentPaidAtColumn(QLStudyDbContext context, string provider)
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            ALTER TABLE "TuitionPayments"
            ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone NULL;
            """);
    }
    else
    {
        var existingColumns = context.Database
            .SqlQueryRaw<string>("SELECT name AS \"Value\" FROM pragma_table_info('TuitionPayments')")
            .ToList();

        if (!existingColumns.Contains("PaidAt"))
        {
            context.Database.ExecuteSqlRaw("ALTER TABLE \"TuitionPayments\" ADD COLUMN \"PaidAt\" TEXT NULL;");
        }
    }
}

static void EnsureTuitionAdjustmentsTable(QLStudyDbContext context, string provider)
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "TuitionAdjustments" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "StudentId" integer NOT NULL,
                "ClassId" integer NOT NULL,
                "TuitionPeriodId" integer NOT NULL,
                "AdjustmentType" text NOT NULL DEFAULT 'None',
                "AdjustmentValue" numeric NOT NULL DEFAULT 0,
                "Note" text NOT NULL DEFAULT '',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "FK_TuitionAdjustments_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TuitionAdjustments_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TuitionAdjustments_TuitionPeriods_TuitionPeriodId" FOREIGN KEY ("TuitionPeriodId") REFERENCES "TuitionPeriods" ("Id") ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TuitionAdjustments_StudentId_ClassId_TuitionPeriodId"
            ON "TuitionAdjustments" ("StudentId", "ClassId", "TuitionPeriodId");
            """);
    }
    else
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "TuitionAdjustments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TuitionAdjustments" PRIMARY KEY AUTOINCREMENT,
                "StudentId" INTEGER NOT NULL,
                "ClassId" INTEGER NOT NULL,
                "TuitionPeriodId" INTEGER NOT NULL,
                "AdjustmentType" TEXT NOT NULL DEFAULT 'None',
                "AdjustmentValue" TEXT NOT NULL DEFAULT '0',
                "Note" TEXT NOT NULL DEFAULT '',
                "CreatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "UpdatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "FK_TuitionAdjustments_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TuitionAdjustments_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_TuitionAdjustments_TuitionPeriods_TuitionPeriodId" FOREIGN KEY ("TuitionPeriodId") REFERENCES "TuitionPeriods" ("Id") ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TuitionAdjustments_StudentId_ClassId_TuitionPeriodId"
            ON "TuitionAdjustments" ("StudentId", "ClassId", "TuitionPeriodId");
            """);
    }
}

static void EnsureStudentClassEnrollmentsTable(QLStudyDbContext context, string provider)
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "StudentClassEnrollments" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "CenterId" integer NOT NULL DEFAULT 1,
                "StudentId" integer NOT NULL,
                "ClassId" integer NOT NULL,
                "StartMonth" text NOT NULL DEFAULT '',
                "EndMonth" text NULL,
                "Status" text NOT NULL DEFAULT 'Active',
                "Reason" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "EndedAt" timestamp with time zone NULL,
                CONSTRAINT "FK_StudentClassEnrollments_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_StudentClassEnrollments_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_StudentClassEnrollments_CenterId" ON "StudentClassEnrollments" ("CenterId");
            CREATE INDEX IF NOT EXISTS "IX_StudentClassEnrollments_StudentId_ClassId" ON "StudentClassEnrollments" ("StudentId", "ClassId");
            CREATE INDEX IF NOT EXISTS "IX_StudentClassEnrollments_ClassId_Status" ON "StudentClassEnrollments" ("ClassId", "Status");

            INSERT INTO "StudentClassEnrollments" ("CenterId", "StudentId", "ClassId", "StartMonth", "Status", "CreatedAt")
            SELECT COALESCE(s."CenterId", 1), sc."StudentId", sc."ClassId", COALESCE(NULLIF(sc."StartMonth", ''), s."StartMonth", 'T1'), 'Active', NOW()
            FROM "StudentClasses" sc
            JOIN "Students" s ON s."Id" = sc."StudentId"
            WHERE NOT EXISTS (
                SELECT 1 FROM "StudentClassEnrollments" e
                WHERE e."StudentId" = sc."StudentId" AND e."ClassId" = sc."ClassId"
            );
            """);
    }
    else
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "StudentClassEnrollments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_StudentClassEnrollments" PRIMARY KEY AUTOINCREMENT,
                "CenterId" INTEGER NOT NULL DEFAULT 1,
                "StudentId" INTEGER NOT NULL,
                "ClassId" INTEGER NOT NULL,
                "StartMonth" TEXT NOT NULL DEFAULT '',
                "EndMonth" TEXT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Active',
                "Reason" TEXT NULL,
                "CreatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "EndedAt" TEXT NULL,
                CONSTRAINT "FK_StudentClassEnrollments_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_StudentClassEnrollments_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_StudentClassEnrollments_CenterId" ON "StudentClassEnrollments" ("CenterId");
            CREATE INDEX IF NOT EXISTS "IX_StudentClassEnrollments_StudentId_ClassId" ON "StudentClassEnrollments" ("StudentId", "ClassId");
            CREATE INDEX IF NOT EXISTS "IX_StudentClassEnrollments_ClassId_Status" ON "StudentClassEnrollments" ("ClassId", "Status");

            INSERT INTO "StudentClassEnrollments" ("CenterId", "StudentId", "ClassId", "StartMonth", "Status", "CreatedAt")
            SELECT COALESCE(s."CenterId", 1), sc."StudentId", sc."ClassId", COALESCE(NULLIF(sc."StartMonth", ''), s."StartMonth", 'T1'), 'Active', CURRENT_TIMESTAMP
            FROM "StudentClasses" sc
            JOIN "Students" s ON s."Id" = sc."StudentId"
            WHERE NOT EXISTS (
                SELECT 1 FROM "StudentClassEnrollments" e
                WHERE e."StudentId" = sc."StudentId" AND e."ClassId" = sc."ClassId"
            );
            """);
    }
}

static void EnsureAnnouncementsTable(QLStudyDbContext context, string provider)
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Announcements" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "CenterId" integer NOT NULL DEFAULT 1,
                "Title" text NOT NULL,
                "Content" text NOT NULL,
                "Type" text NOT NULL DEFAULT 'Center',
                "ClassId" integer NULL,
                "StartDate" timestamp with time zone NULL,
                "EndDate" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT "FK_Announcements_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Announcements_CenterId" ON "Announcements" ("CenterId");
            CREATE INDEX IF NOT EXISTS "IX_Announcements_ClassId" ON "Announcements" ("ClassId");
            """);
    }
    else
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Announcements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Announcements" PRIMARY KEY AUTOINCREMENT,
                "CenterId" INTEGER NOT NULL DEFAULT 1,
                "Title" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "Type" TEXT NOT NULL DEFAULT 'Center',
                "ClassId" INTEGER NULL,
                "StartDate" TEXT NULL,
                "EndDate" TEXT NULL,
                "CreatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT "FK_Announcements_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_Announcements_CenterId" ON "Announcements" ("CenterId");
            CREATE INDEX IF NOT EXISTS "IX_Announcements_ClassId" ON "Announcements" ("ClassId");
            """);
    }

    if (!context.Announcements.Any())
    {
        context.Announcements.AddRange(
            new Announcement
            {
                CenterId = 1,
                Title = "Nghỉ lễ Quốc Khánh",
                Content = "Trung tâm nghỉ lễ Quốc Khánh từ ngày 02/09 đến hết ngày 04/09/2026. Lớp học bù sẽ được thông báo sau.",
                Type = "Center",
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(30)
            },
            new Announcement
            {
                CenterId = 1,
                Title = "Kiểm tra học kỳ môn Toán",
                Content = "Lịch kiểm tra học kỳ định kỳ môn Toán sẽ diễn ra vào tuần tới ngày 15/08. Học sinh vui lòng ôn tập tốt.",
                Type = "Center",
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(7)
            }
        );
        context.SaveChanges();
    }
}

static void EnsureCenterPaymentColumns(QLStudyDbContext context, string provider)
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "PaymentQrCode" text NULL;
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "BankAccountNumber" text NULL;
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "BankName" text NULL;
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "BankAccountName" text NULL;
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "PayOSClientId" text NULL;
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "PayOSApiKey" text NULL;
            ALTER TABLE "Centers" ADD COLUMN IF NOT EXISTS "PayOSChecksumKey" text NULL;
        """);
    }
    else
    {
        var existingColumns = context.Database
            .SqlQueryRaw<string>($"""SELECT name AS "Value" FROM pragma_table_info('Centers')""")
            .ToList();

        if (!existingColumns.Contains("PaymentQrCode"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "PaymentQrCode" TEXT NULL;""");
        if (!existingColumns.Contains("BankAccountNumber"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "BankAccountNumber" TEXT NULL;""");
        if (!existingColumns.Contains("BankName"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "BankName" TEXT NULL;""");
        if (!existingColumns.Contains("BankAccountName"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "BankAccountName" TEXT NULL;""");
        if (!existingColumns.Contains("PayOSClientId"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "PayOSClientId" TEXT NULL;""");
        if (!existingColumns.Contains("PayOSApiKey"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "PayOSApiKey" TEXT NULL;""");
        if (!existingColumns.Contains("PayOSChecksumKey"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "Centers" ADD COLUMN "PayOSChecksumKey" TEXT NULL;""");
    }
}

static void EnsurePayOSTransactionsTable(QLStudyDbContext context, string provider)
{
    if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PayOSTransactions" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "CenterId" integer NOT NULL DEFAULT 1,
                "StudentId" integer NOT NULL,
                "ClassId" integer NULL,
                "TuitionPeriodId" integer NOT NULL,
                "Amount" numeric NOT NULL,
                "Status" text NOT NULL DEFAULT 'Pending',
                "PaymentLinkId" text NULL,
                "CheckoutUrl" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "PaidAt" timestamp with time zone NULL,
                CONSTRAINT "FK_PayOSTransactions_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PayOSTransactions_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PayOSTransactions_TuitionPeriods_TuitionPeriodId" FOREIGN KEY ("TuitionPeriodId") REFERENCES "TuitionPeriods" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PayOSTransactions_CenterId" ON "PayOSTransactions" ("CenterId");
            CREATE INDEX IF NOT EXISTS "IX_PayOSTransactions_Status" ON "PayOSTransactions" ("Status");
        """);
    }
    else
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PayOSTransactions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PayOSTransactions" PRIMARY KEY AUTOINCREMENT,
                "CenterId" INTEGER NOT NULL DEFAULT 1,
                "StudentId" INTEGER NOT NULL,
                "ClassId" INTEGER NULL,
                "TuitionPeriodId" INTEGER NOT NULL,
                "Amount" TEXT NOT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Pending',
                "PaymentLinkId" TEXT NULL,
                "CheckoutUrl" TEXT NULL,
                "CreatedAt" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "PaidAt" TEXT NULL,
                CONSTRAINT "FK_PayOSTransactions_Students_StudentId" FOREIGN KEY ("StudentId") REFERENCES "Students" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PayOSTransactions_Classes_ClassId" FOREIGN KEY ("ClassId") REFERENCES "Classes" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PayOSTransactions_TuitionPeriods_TuitionPeriodId" FOREIGN KEY ("TuitionPeriodId") REFERENCES "TuitionPeriods" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_PayOSTransactions_CenterId" ON "PayOSTransactions" ("CenterId");
            CREATE INDEX IF NOT EXISTS "IX_PayOSTransactions_Status" ON "PayOSTransactions" ("Status");
        """);
    }
}

