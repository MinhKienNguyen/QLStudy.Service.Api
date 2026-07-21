using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
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
        policy.WithOrigins("http://localhost:4200")
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

        // Seed default subjects, admin account, screen permissions, and update existing classes
        DbInitializer.Initialize(context);
        Console.WriteLine("RBAC and Subject config initialized successfully!");

        var seedExcelOnStartup = builder.Configuration.GetValue<bool>("SeedExcelOnStartup");

        // Seed data if explicitly enabled and database is empty
        if (seedExcelOnStartup && !context.Semesters.Any())
        {
            var seeder = services.GetRequiredService<ExcelSeeder>();
            string excelPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "lịch dạy.xlsx"));
            seeder.Seed(excelPath);
            // Re-run initializer to map the newly imported classes to subjects
            DbInitializer.Initialize(context);
            Console.WriteLine("Database auto-seeded from lịch dạy.xlsx successfully!");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred during database migration or seeding: {ex.Message}");
    }
}

app.Run();

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
