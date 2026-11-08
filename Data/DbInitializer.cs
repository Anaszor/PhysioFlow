using Microsoft.AspNetCore.Identity;
using PhysioClinic.Models;

namespace PhysioClinic.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(PhysioDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            context.Database.EnsureCreated();

            // 1. Seed Roles
            foreach (var roleName in ClinicRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Therapists first if not present
            List<Therapist> therapists;
            if (!context.Therapists.Any())
            {
                therapists = new List<Therapist>
                {
                    new Therapist
                    {
                        FullName = "د. فراس الخالدي",
                        Title = "استشاري أول علاج طبيعي وتأهيل رياضي",
                        Specialization = "إصابات الملاعب والعمود الفقري",
                        Phone = "+962 7 9123 4567",
                        Email = "firas@physiocare.com",
                        ColorTag = "#0d9488", // Teal
                        IsActive = true
                    },
                    new Therapist
                    {
                        FullName = "أ. سارة التميمي",
                        Title = "أخصائية علاج يدوي وتأهيل مفاصل",
                        Specialization = "تأهيل ما بعد الجراحات واستبدال المفاصل",
                        Phone = "+962 7 8123 4568",
                        Email = "sara@physiocare.com",
                        ColorTag = "#6366f1", // Indigo
                        IsActive = true
                    },
                    new Therapist
                    {
                        FullName = "د. طارق المنصور",
                        Title = "أخصائي تأهيل عصبي وعضلي",
                        Specialization = "العلاج بالابر الجافة والموجات التصادمية",
                        Phone = "+962 7 7123 4569",
                        Email = "tariq@physiocare.com",
                        ColorTag = "#f59e0b", // Amber
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

            // 3. Seed Staff User Accounts (Admin, Receptionist, Doctor)
            // Admin: Full Access
            if (await userManager.FindByEmailAsync("admin@physioflow.com") == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin@physioflow.com",
                    Email = "admin@physioflow.com",
                    FullName = "عبد العزيز آل الشيخ",
                    RoleTitle = "مدير عام المركز الطبي",
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
                    FullName = "نورة العتيبي",
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

            // Doctor: Dr. Firas
            if (await userManager.FindByEmailAsync("firas@physioflow.com") == null)
            {
                var docUser = new ApplicationUser
                {
                    UserName = "firas@physioflow.com",
                    Email = "firas@physioflow.com",
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

            // Doctor: Sara
            if (await userManager.FindByEmailAsync("sara@physioflow.com") == null)
            {
                var docUser2 = new ApplicationUser
                {
                    UserName = "sara@physioflow.com",
                    Email = "sara@physioflow.com",
                    FullName = "أ. سارة التميمي",
                    RoleTitle = "أخصائية علاج يدوي وتأهيل مفاصل",
                    TherapistId = therapists.Skip(1).FirstOrDefault()?.Id,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(docUser2, "Doctor@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(docUser2, ClinicRoles.Doctor);
                }
            }

            // 4. Seed Patients / Clients & Medical History
            if (context.Clients.Any())
            {
                return; // Clients and clinical data already seeded
            }

            var client1 = new Client
            {
                FullName = "محمد عبد الرحمن القحطاني",
                Phone = "+966 50 123 4567",
                Email = "m.qahtani@example.com",
                Gender = "Male",
                DateOfBirth = new DateTime(1992, 5, 14),
                Occupation = "مهندس برمجيات (عمل مكتبي 9 ساعات)",
                Notes = "الرياض - حي الياسمين",
                EmergencyContact = "خالد القحطاني (شقيق) - 0509998877",
                CreatedAt = DateTime.UtcNow.AddDays(-18)
            };

            var client2 = new Client
            {
                FullName = "رنيم عبد العزيز الشريف",
                Phone = "+966 55 987 6543",
                Email = "raneem.sh@example.com",
                Gender = "Female",
                DateOfBirth = new DateTime(1998, 11, 20),
                Occupation = "لاعبة بادل ومدربة لياقة",
                Notes = "جدة - حي الروضة",
                EmergencyContact = "أمينة الشريف (والدة) - 0551122334",
                CreatedAt = DateTime.UtcNow.AddDays(-12)
            };

            var client3 = new Client
            {
                FullName = "سليمان بن ناصر العتيبي",
                Phone = "+966 54 455 3333",
                Email = "s.otaibi@example.com",
                Gender = "Male",
                DateOfBirth = new DateTime(1985, 3, 30),
                Occupation = "مدير مالي",
                Notes = "الدمام - حي الشاطئ",
                EmergencyContact = "فيصل العتيبي - 0540001122",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            context.Clients.AddRange(client1, client2, client3);
            await context.SaveChangesAsync();

            // 5. Seed Intake Forms
            var intake1 = new IntakeForm
            {
                ClientId = client1.Id,
                BodyPart = "أسفل الظهر (Lumbar Spine)",
                InjuryType = "انزلاق غضروفي قطني (Disc Bulge L4-L5) وتيبس أسفل الظهر",
                PainLevel = 7,
                Symptoms = "ألم حاد ممتد للمؤخرة، تنميل خفيف في الفخذ الأيسر عند الجلوس المطول. تقرير MRI: بروز غضروفي خفيف L4-L5 ملامس للجذر العصبي.",
                HasRadiologyReports = true,
                CreatedAt = DateTime.UtcNow.AddDays(-18)
            };

            var intake2 = new IntakeForm
            {
                ClientId = client2.Id,
                BodyPart = "الركبة اليمنى (Right Knee)",
                InjuryType = "التواء حاد وتمزق جزئي في الرباط الصليبي والأربطة الجانبية (MCL)",
                PainLevel = 8,
                Symptoms = "تورم واضح، عدم استقرار عند النزول من الدرج، فرقعة عند الثني. رنين مغناطيسي Knee MRI: تمزق جزئي درجة ثانية في MCL.",
                HasRadiologyReports = true,
                CreatedAt = DateTime.UtcNow.AddDays(-12)
            };

            var intake3 = new IntakeForm
            {
                ClientId = client3.Id,
                BodyPart = "الكتف الأيمن (Right Shoulder)",
                InjuryType = "التهاب أوتار الكفة المدورة وتيبس كتف (Rotator Cuff Impingement)",
                PainLevel = 6,
                Symptoms = "صعوبة رفع الذراع للأعلى فوق الرأس وألم ليلي عند النوم عليه.",
                HasRadiologyReports = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            };

            context.IntakeForms.AddRange(intake1, intake2, intake3);
            await context.SaveChangesAsync();

            // 6. Seed Consultations
            var consult1 = new Consultation
            {
                ClientId = client1.Id,
                TherapistId = therapists[0].Id,
                ConsultationDate = DateTime.Today.AddDays(-17),
                ChiefComplaint = "ألم مزمن أسفل الظهر يعيق العمل المكتبي لأكثر من 30 دقيقة متواصلة.",
                RangeOfMotionFindings = "ثني الجذع محدود بنسبة 40% مصحوب بألم. بسط الظهر مقبول.",
                MuscleStrengthTests = "اختبار رفع الساق مستقيمة (SLR) إيجابي عند 60 درجة في الطرف الأيسر. ضعف خفيف في عضلات الجذع العميقة (Core).",
                Diagnosis = "متلازمة الألم القطني الميكانيكي مع اعتلال جذري خفيف L4-L5 غير جراحي.",
                TreatmentGoals = "تخفيف الألم إلى درجة 2/10، استعادة مرونة الجذع، تقوية عضلات البطن وأسفل الظهر العميقة.",
                ClinicalNotes = "المريض يستجيب جيداً للعلاج التحفظي مع التوصية بكرسي مريح وتغيير وضعية الجلوس دورياً.",
                CreatedAt = DateTime.UtcNow.AddDays(-17)
            };

            var consult2 = new Consultation
            {
                ClientId = client2.Id,
                TherapistId = therapists[1].Id,
                ConsultationDate = DateTime.Today.AddDays(-11),
                ChiefComplaint = "ألم وتورم بعد حركة مفاجئة أثناء مباراة بادل وعدم القدرة على ارتكاز الوزن.",
                RangeOfMotionFindings = "مدى الثني محدود عند 85 درجة نتيجة تجمع السوائل. البسط كامل.",
                MuscleStrengthTests = "Lachman test إيجابي خفيف. ألم عند ضغط الحافة الأنسية للمفصل. ضعف 3/5 في العضلة الرباعية.",
                Diagnosis = "التواء الركبة الحاد مع إصابة MCL من الدرجة الثانية وضعف استقرار الرضفة.",
                TreatmentGoals = "امتصاص الورم، استعادة المدى الحركي الكامل (0 - 135 درجة)، استعادة القوة والتوازن الحركي.",
                ClinicalNotes = "تم وضع جبيرة ضاغطة خفيفة والبدء بتمارين الانقباض الثابت (Isometric).",
                CreatedAt = DateTime.UtcNow.AddDays(-11)
            };

            context.Consultations.AddRange(consult1, consult2);
            await context.SaveChangesAsync();

            // 7. Seed Treatment Plans
            var plan1 = new TreatmentPlan
            {
                ClientId = client1.Id,
                TherapistId = therapists[0].Id,
                ConsultationId = consult1.Id,
                Title = "برنامج تأهيل الانزلاق القطني وتقوية الجذع (12 جلسة)",
                TotalSessions = 12,
                CompletedSessions = 5,
                Frequency = "3 جلسات أسبوعياً (أحد - ثلاثاء - خميس)",
                Modalities = "علاج يدوي (Manual Mobilization)، تيار TENS مسكن، سحب فقرات قطني خفيف (Traction)، تمارين استقرار الجذع (Core Stability)",
                MedicationAndHomeCare = "مرخي عضلات عند النوم لمدة 5 أيام حسب وصفة الطبيب، كمادات باردة بعد التمارين 15 دقيقة، دعامة ظهر قطنية أثناء القيادة فقط.",
                HomeExercises = "1. تمرين مكينزي للبسط (McKenzie Extensions) 10 تكرارات كل ساعتين.\n2. تمرين الجسر (Bridge Exercise) 3 مجموعات × 10 ثواني ثبات.\n3. تمرين القطة والجمل لتليين الفقرات صباحاً.",
                Status = "Active",
                StartDate = DateTime.Today.AddDays(-15),
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            };

            var plan2 = new TreatmentPlan
            {
                ClientId = client2.Id,
                TherapistId = therapists[1].Id,
                ConsultationId = consult2.Id,
                Title = "برنامج تأهيل أربطة الركبة والعودة للنشاط الرياضي (16 جلسة)",
                TotalSessions = 16,
                CompletedSessions = 3,
                Frequency = "3 جلسات أسبوعياً",
                Modalities = "تصريف التورم اللمفاوي، علاج ليزر عالي الشدة (High-Intensity Laser)، تحفيز عضلي كهربائي (EMS) للرباعية، تمارين توازن حركي (Proprioception)",
                MedicationAndHomeCare = "تطبيق الثلج مرتين يومياً، ارتداء المشد الضاغط أثناء المشي، تجنب ثني الركبة الحاد أو الالتفاف.",
                HomeExercises = "1. رفع الساق مستقيمة (Straight Leg Raise) 3 × 15 تكرار.\n2. قبض العضلة الرباعية بضغط منشفة تحت الركبة (Quad Sets).\n3. تمرين المضخة للكاحل لتحسين الدورة الدموية.",
                Status = "Active",
                StartDate = DateTime.Today.AddDays(-8),
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            };

            context.TreatmentPlans.AddRange(plan1, plan2);
            await context.SaveChangesAsync();

            // 8. Seed Appointments (Today and Upcoming)
            var appts = new List<Appointment>
            {
                new Appointment
                {
                    ClientId = client1.Id,
                    TherapistId = therapists[0].Id,
                    TreatmentPlanId = plan1.Id,
                    AppointmentDate = DateTime.Today,
                    StartTime = "10:00",
                    EndTime = "10:45",
                    SessionNumber = 6,
                    Status = "Confirmed",
                    SessionNotes = "جلسة تقوية واستمرار على خطة الجذع.",
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Appointment
                {
                    ClientId = client2.Id,
                    TherapistId = therapists[1].Id,
                    TreatmentPlanId = plan2.Id,
                    AppointmentDate = DateTime.Today,
                    StartTime = "11:30",
                    EndTime = "12:15",
                    SessionNumber = 4,
                    Status = "Scheduled",
                    SessionNotes = "التركيز على مدى الثني وبدء تمارين التوازن الحركي.",
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Appointment
                {
                    ClientId = client3.Id,
                    TherapistId = therapists[2].Id,
                    TreatmentPlanId = null,
                    AppointmentDate = DateTime.Today,
                    StartTime = "16:00",
                    EndTime = "16:45",
                    SessionNumber = 1,
                    Status = "Scheduled",
                    SessionNotes = "استشارة أولى وتحديد خطة علاج الكتف وتطبيق جلسة إبر جافة.",
                    CreatedAt = DateTime.UtcNow.AddHours(-5)
                },
                new Appointment
                {
                    ClientId = client1.Id,
                    TherapistId = therapists[0].Id,
                    TreatmentPlanId = plan1.Id,
                    AppointmentDate = DateTime.Today.AddDays(1),
                    StartTime = "10:00",
                    EndTime = "10:45",
                    SessionNumber = 7,
                    Status = "Scheduled",
                    SessionNotes = null,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Appointment
                {
                    ClientId = client2.Id,
                    TherapistId = therapists[1].Id,
                    TreatmentPlanId = plan2.Id,
                    AppointmentDate = DateTime.Today.AddDays(1),
                    StartTime = "11:30",
                    EndTime = "12:15",
                    SessionNumber = 5,
                    Status = "Scheduled",
                    SessionNotes = null,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            context.Appointments.AddRange(appts);
            await context.SaveChangesAsync();
        }
    }
}
