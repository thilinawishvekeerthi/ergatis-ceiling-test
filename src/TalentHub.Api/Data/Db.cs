using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TalentHub.Api.Domain;

namespace TalentHub.Api.Data;

public static class Db
{
    public static void Migrate(TalentHubDbContext db)
    {
        // No design-time migrations are shipped; with none present EnsureCreated bootstraps
        // the schema (including Identity tables), otherwise migrations win.
        if (db.Database.IsRelational() && db.Database.GetMigrations().Any()) db.Database.Migrate();
        else db.Database.EnsureCreated();
    }

    public static async Task SeedAsync(TalentHubDbContext db, IConfiguration cfg)
    {
        if (!await db.Users.AnyAsync())
        {
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Employee>();
            string Hash(Employee u, string pwd) => hasher.HashPassword(u, pwd);
            var admin = new Employee
            {
                UserName = "admin", FullName = "HR Administrator",
                Email = "admin@talenthub.local", EmailConfirmed = true
            };
            admin.PasswordHash = Hash(admin, "Admin@123");
            var s1 = new Employee { UserName = "supervisor1", FullName = "Sarah Supervisor", Email = "sarah@talenthub.local" };
            s1.PasswordHash = Hash(s1, "Pass@123");
            var s2 = new Employee { UserName = "supervisor2", FullName = "Sam Supervisor", Email = "sam@talenthub.local" };
            s2.PasswordHash = Hash(s2, "Pass@123");
            var e1 = new Employee { UserName = "employee1", FullName = "Ella Employee", Email = "ella@talenthub.local", Supervisor = s1 };
            e1.PasswordHash = Hash(e1, "Pass@123");
            var e2 = new Employee { UserName = "employee2", FullName = "Evan Employee", Email = "evan@talenthub.local", Supervisor = s1 };
            e2.PasswordHash = Hash(e2, "Pass@123");
            var e3 = new Employee { UserName = "employee3", FullName = "Evanora Employee", Email = "evanora@talenthub.local", Supervisor = s2 };
            e3.PasswordHash = Hash(e3, "Pass@123");
            var r1 = new Employee { UserName = "reviewer1", FullName = "Rita Reviewer", Email = "rita@talenthub.local" };
            r1.PasswordHash = Hash(r1, "Pass@123");
            var b1 = new Employee { UserName = "board1", FullName = "Board Member One", Email = "board1@talenthub.local" };
            b1.PasswordHash = Hash(b1, "Pass@123");

            var fams = new Dictionary<string, (string[] Jobs, string[] Alts)>
            {
                ["Engineering"] = (["Software Engineer", "Senior Software Engineer", "Principal Engineer"], ["Product", "Data"]),
                ["Product"] = (["Product Analyst", "Product Manager"], ["Engineering"]),
                ["Data"] = (["Data Analyst", "Data Scientist"], ["Engineering"]),
                ["People Ops"] = (["HR Officer", "HR Manager", "HRO Payroll"], ["Product"]),
            };
            var famMap = new Dictionary<string, JobFamily>();
            foreach (var (name, spec) in fams)
            {
                var fam = new JobFamily { Name = name };
                foreach (var j in spec.Jobs) fam.Jobs.Add(new Job { Title = j });
                famMap[name] = fam;
                db.JobFamilies.Add(fam);
            }
            foreach (var (name, spec) in fams)
                foreach (var alt in spec.Alts)
                    db.AlternativeCareerPaths.Add(new AlternativeCareerPath { FromJobFamily = famMap[name], ToJobFamily = famMap[alt] });

            e1.Job = famMap["Engineering"].Jobs.ElementAt(0);
            e2.Job = famMap["Engineering"].Jobs.ElementAt(1);
            e3.Job = famMap["Data"].Jobs.ElementAt(0);
            r1.Job = famMap["Product"].Jobs.ElementAt(1);
            s1.Job = famMap["Engineering"].Jobs.ElementAt(2);

            // Normalize identity columns: seeds bypass UserManager, so FindByNameAsync
            // (which queries NormalizedUserName) would otherwise never match.
            var normalizer = new Microsoft.AspNetCore.Identity.UpperInvariantLookupNormalizer();
            foreach (var u in new[] { admin, s1, s2, e1, e2, e3, r1, b1 })
            {
                u.NormalizedUserName = normalizer.NormalizeName(u.UserName);
                u.NormalizedEmail = normalizer.NormalizeEmail(u.Email);
            }

            var scale = new Scale
            {
                Name = "Performance 3-point",
                Levels = { new ScaleLevel { Label = "Low", Value = 1 }, new ScaleLevel { Label = "Medium", Value = 2 }, new ScaleLevel { Label = "High", Value = 3 } }
            };
            db.Scales.Add(scale);

            db.Users.AddRange(admin, s1, s2, e1, e2, e3, r1, b1);
            await db.SaveChangesAsync();

            var roles = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id);
            foreach (var (u, role) in new[] { (admin, AppRole.HrAdmin), (s1, AppRole.Supervisor), (s2, AppRole.Supervisor), (e1, AppRole.Employee), (e2, AppRole.Employee), (e3, AppRole.Employee), (r1, AppRole.Reviewer), (b1, AppRole.ReviewBoard) })
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = u.Id, RoleId = roles[role.ToString()] });

            var cycle = new TalentReviewCycle
            {
                Name = "Annual Review 2026",
                StartDate = new DateOnly(DateTime.UtcNow.Year, 1, 1),
                EndDate = new DateOnly(DateTime.UtcNow.Year, 12, 31),
                Scale = scale,
                Competencies = { new CycleCompetency { Name = "Delivery" }, new CycleCompetency { Name = "Collaboration" }, new CycleCompetency { Name = "Leadership" } }
            };
            db.TalentReviewCycles.Add(cycle);
            db.DevelopmentPlanConfigs.Add(new DevelopmentPlanConfig
            {
                ReviewFrequency = ReviewFrequency.Quarterly,
                ReviewPeriodMonths = 12,
                OverallRatingMethod = OverallRatingMethod.AverageOfCompetencies
            });
            db.CriticalPositions.Add(new CriticalPosition { Title = "Head of Engineering", Department = "Engineering", IncumbentEmployee = s1 });
            await db.SaveChangesAsync();
        }
    }
}
