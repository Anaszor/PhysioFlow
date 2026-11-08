using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;

namespace PhysioClinic.Controllers
{
    [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Doctor}")]
    public class ConsultationController : Controller
    {
        private readonly PhysioDbContext _context;

        public ConsultationController(PhysioDbContext context)
        {
            _context = context;
        }

        // GET: Consultation/Create?clientId=5
        public async Task<IActionResult> Create(int? clientId)
        {
            var consultation = new Consultation
            {
                ConsultationDate = DateTime.Today
            };

            if (clientId.HasValue)
            {
                consultation.ClientId = clientId.Value;
                var client = await _context.Clients
                    .Include(c => c.IntakeForms)
                    .FirstOrDefaultAsync(c => c.Id == clientId.Value);

                if (client != null)
                {
                    ViewBag.Client = client;
                    var latestIntake = client.IntakeForms.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
                    if (latestIntake != null)
                    {
                        consultation.ChiefComplaint = $"شكوى من ألم في {latestIntake.BodyPart} - ({latestIntake.InjuryType}). شدة الألم: {latestIntake.PainLevel}/10.";
                    }
                }
            }

            ViewBag.Clients = new SelectList(await _context.Clients.OrderBy(c => c.FullName).ToListAsync(), "Id", "FullName", clientId);
            ViewBag.Therapists = new SelectList(await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync(), "Id", "FullName");

            return View(consultation);
        }

        // POST: Consultation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Consultation consultation)
        {
            if (ModelState.IsValid)
            {
                consultation.CreatedAt = DateTime.UtcNow;
                _context.Consultations.Add(consultation);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم تسجيل الاستشارة والتشخيص السريري بنجاح!";
                return RedirectToAction("Details", "Clients", new { id = consultation.ClientId });
            }

            ViewBag.Clients = new SelectList(await _context.Clients.OrderBy(c => c.FullName).ToListAsync(), "Id", "FullName", consultation.ClientId);
            ViewBag.Therapists = new SelectList(await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync(), "Id", "FullName", consultation.TherapistId);
            return View(consultation);
        }

        // GET: Consultation/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var consultation = await _context.Consultations
                .Include(c => c.Client)
                .Include(c => c.Therapist)
                .Include(c => c.TreatmentPlans)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (consultation == null) return NotFound();

            return View(consultation);
        }
    }
}
