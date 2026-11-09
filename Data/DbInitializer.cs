using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Models;

namespace PhysioClinic.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(PhysioDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            context.Database.EnsureCreated();

            // Safe column additions for Appointments table if schema was created earlier
            try
            {
                var conn = context.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info(Appointments);";
                using var reader = await checkCmd.ExecuteReaderAsync();
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync())
                {
                    cols.Add(reader.GetString(1));
                }
                await reader.CloseAsync();

                async Task AddColIfMissing(string col, string type)
                {
                    if (!cols.Contains(col))
                    {
                        using var alterCmd = conn.CreateCommand();
                        alterCmd.CommandText = $"ALTER TABLE Appointments ADD COLUMN {col} {type};";
                        await alterCmd.ExecuteNonQueryAsync();
                    }
                }

                await AddColIfMissing("PatientSignature", "TEXT");
                await AddColIfMissing("PatientResponse", "TEXT");
                await AddColIfMissing("ClinicalNotes", "TEXT");
                await AddColIfMissing("NextSessionPlan", "TEXT");
            }
            catch { /* Handled silently for non-SQLite or existing columns */ }

            // 1. Seed Roles
            foreach (var roleName in ClinicRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed ONE Demo Doctor/Therapist
            List<Therapist> therapists;
            if (!context.Therapists.Any())
            {
                therapists = new List<Therapist>
                {
                    new Therapist
                    {
                        FullName = "د. فراس الخالدي",
                        Title = "استشاري أول علاج طبيعي وتأهيل رياضي",
                        Specialization = "إصابات الملاعب والعمود الفقري والتأهيل الحركي",
                        Phone = "+962 7 9123 4567",
                        Email = "doctor@physioflow.com",
                        ColorTag = "#0d9488", // Teal
                        IsActive = true
                    }
                };
                context.Therapists.AddRange(therapists);
                await context.SaveChangesAsync();
            }
            else
            {
                therapists = context.Therapists.ToList();
            }

            // 3. Seed Exactly 3 Core Accounts: Admin, Receptionist, 1 Demo Doctor
            // Admin: Full Access
            if (await userManager.FindByEmailAsync("admin@physioflow.com") == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin@physioflow.com",
                    Email = "admin@physioflow.com",
                    FullName = "د. عمر العدوان",
                    RoleTitle = "المدير العام والمسؤول الطبي",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, ClinicRoles.Admin);
                }
            }

            // Receptionist: Appointments & Intake only
            if (await userManager.FindByEmailAsync("reception@physioflow.com") == null)
            {
                var recepUser = new ApplicationUser
                {
                    UserName = "reception@physioflow.com",
                    Email = "reception@physioflow.com",
                    FullName = "ديمة الزعبي",
                    RoleTitle = "مسؤولة الاستقبال وتنسيق المواعيد",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(recepUser, "Recep@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(recepUser, ClinicRoles.Receptionist);
                }
            }

            // One Demo Doctor: Dr. Firas
            if (await userManager.FindByEmailAsync("doctor@physioflow.com") == null)
            {
                var docUser = new ApplicationUser
                {
                    UserName = "doctor@physioflow.com",
                    Email = "doctor@physioflow.com",
                    FullName = "د. فراس الخالدي",
                    RoleTitle = "استشاري أول علاج طبيعي وتأهيل رياضي",
                    TherapistId = therapists.FirstOrDefault()?.Id,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(docUser, "Doctor@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(docUser, ClinicRoles.Doctor);
                }
            }

            // All demo clients, intake forms, consultations, plans, and appointments are CLEARED.
            // Ready for real clinic patient data.
        }
    }
}
