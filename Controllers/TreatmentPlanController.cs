using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;

namespace PhysioClinic.Controllers
{
    [Authorize]
    public class TreatmentPlanController : Controller
    {
        private readonly PhysioDbContext _context;

        public TreatmentPlanController(PhysioDbContext context)
        {
            _context = context;
        }

        // GET: TreatmentPlan
        public async Task<IActionResult> Index(string? status)
        {
            var query = _context.TreatmentPlans
                .Include(tp => tp.Client)
                .Include(tp => tp.Therapist)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(tp => tp.Status == status);
                ViewBag.CurrentStatus = status;
            }

            var plans = await query
                .OrderByDescending(tp => tp.CreatedAt)
                .ToListAsync();

            return View(plans);
        }

        // GET: TreatmentPlan/Create?clientId=5&consultationId=2
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Doctor}")]
        public async Task<IActionResult> Create(int? clientId, int? consultationId)
        {
            var plan = new TreatmentPlan
            {
                StartDate = DateTime.Today,
                ExpectedEndDate = DateTime.Today.AddDays(30),
                TotalSessions = 10,
                Frequency = "3 جلسات أسبوعياً",
                Status = "Active"
            };

            if (clientId.HasValue)
            {
                plan.ClientId = clientId.Value;
                var client = await _context.Clients
                    .Include(c => c.Consultations)
                    .FirstOrDefaultAsync(c => c.Id == clientId.Value);

                if (client != null)
                {
                    ViewBag.Client = client;
                }
            }

            if (consultationId.HasValue)
            {
                plan.ConsultationId = consultationId.Value;
                var consultation = await _context.Consultations.FindAsync(consultationId.Value);
                if (consultation != null)
                {
                    plan.TherapistId = consultation.TherapistId;
                    plan.Title = $"خطة علاج: {consultation.Diagnosis}";
                }
            }

            ViewBag.Clients = new SelectList(await _context.Clients.OrderBy(c => c.FullName).ToListAsync(), "Id", "FullName", clientId);
            ViewBag.Therapists = new SelectList(await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync(), "Id", "FullName", plan.TherapistId);
            ViewBag.Consultations = new SelectList(await _context.Consultations.OrderByDescending(c => c.ConsultationDate).ToListAsync(), "Id", "Diagnosis", consultationId);

            return View(plan);
        }

        // POST: TreatmentPlan/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Doctor}")]
        public async Task<IActionResult> Create(TreatmentPlan plan)
        {
            if (ModelState.IsValid)
            {
                plan.CreatedAt = DateTime.UtcNow;
                _context.TreatmentPlans.Add(plan);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم إنشاء وتفعيل الخطة العلاجية بنجاح!";
                return RedirectToAction(nameof(Details), new { id = plan.Id });
            }

            ViewBag.Clients = new SelectList(await _context.Clients.OrderBy(c => c.FullName).ToListAsync(), "Id", "FullName", plan.ClientId);
            ViewBag.Therapists = new SelectList(await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync(), "Id", "FullName", plan.TherapistId);
            ViewBag.Consultations = new SelectList(await _context.Consultations.OrderByDescending(c => c.ConsultationDate).ToListAsync(), "Id", "Diagnosis", plan.ConsultationId);
            return View(plan);
        }

        // GET: TreatmentPlan/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var plan = await _context.TreatmentPlans
                .Include(tp => tp.Client)
                .Include(tp => tp.Therapist)
                .Include(tp => tp.Consultation)
                .Include(tp => tp.Appointments.OrderByDescending(a => a.AppointmentDate))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (plan == null) return NotFound();

            return View(plan);
        }

        // POST: TreatmentPlan/IncrementSession/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Doctor}")]
        public async Task<IActionResult> IncrementSession(int id)
        {
            var plan = await _context.TreatmentPlans.FindAsync(id);
            if (plan != null)
            {
                if (plan.CompletedSessions < plan.TotalSessions)
                {
                    plan.CompletedSessions++;
                    if (plan.CompletedSessions >= plan.TotalSessions)
                    {
                        plan.Status = "Completed";
                    }
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"تم تسجيل إتمام الجلسة رقم {plan.CompletedSessions} بنجاح!";
                }
            }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
