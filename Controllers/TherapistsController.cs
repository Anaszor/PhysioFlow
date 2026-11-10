using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;

namespace PhysioClinic.Controllers
{
    [Authorize]
    public class TherapistsController : Controller
    {
        private readonly PhysioDbContext _context;

        public TherapistsController(PhysioDbContext context)
        {
            _context = context;
        }

        // GET: Therapists
        public async Task<IActionResult> Index()
        {
            var therapists = await _context.Therapists
                .Include(t => t.Appointments)
                .Include(t => t.Consultations)
                .Include(t => t.TreatmentPlans)
                .OrderByDescending(t => t.IsActive)
                .ThenBy(t => t.FullName)
                .ToListAsync();

            return View(therapists);
        }

        // GET: Therapists/Create
        [Authorize(Roles = ClinicRoles.Admin)]
        public IActionResult Create()
        {
            return View(new Therapist
            {
                ColorTag = "#0d9488",
                IsActive = true
            });
        }

        // POST: Therapists/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Create([Bind("FullName,Title,Specialization,Phone,Email,ColorTag,IsActive")] Therapist therapist)
        {
            if (ModelState.IsValid)
            {
                _context.Add(therapist);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تمت إضافة الطبيب/الأخصائي ({therapist.FullName}) بنجاح!";
                return RedirectToAction(nameof(Index));
            }
            return View(therapist);
        }

        // GET: Therapists/Edit/5
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var therapist = await _context.Therapists.FindAsync(id);
            if (therapist == null) return NotFound();

            return View(therapist);
        }

        // POST: Therapists/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Title,Specialization,Phone,Email,ColorTag,IsActive")] Therapist therapist)
        {
            if (id != therapist.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(therapist);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"تم تحديث بيانات الطبيب/الأخصائي ({therapist.FullName}) بنجاح!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Therapists.Any(e => e.Id == therapist.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(therapist);
        }

        // POST: Therapists/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var therapist = await _context.Therapists.FindAsync(id);
            if (therapist == null) return NotFound();

            therapist.IsActive = !therapist.IsActive;
            await _context.SaveChangesAsync();

            var statusStr = therapist.IsActive ? "تفعيل" : "تعطيل";
            TempData["SuccessMessage"] = $"تم {statusStr} حساب الطبيب ({therapist.FullName}) بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Therapists/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var therapist = await _context.Therapists
                .Include(t => t.Appointments)
                .Include(t => t.Consultations)
                .Include(t => t.TreatmentPlans)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (therapist == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على الأخصائي المطلوب.";
                return RedirectToAction(nameof(Index));
            }

            // Check if therapist has linked records
            if (therapist.Appointments.Any() || therapist.Consultations.Any() || therapist.TreatmentPlans.Any())
            {
                // Can't hard delete due to database integrity constraints
                therapist.IsActive = false;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم تعطيل وتجميد حساب الطبيب ({therapist.FullName}) بنجاح حفاظاً على سلامة السجلات الطبية للمرضى والجلسات السابقة.";
            }
            else
            {
                var name = therapist.FullName;
                _context.Therapists.Remove(therapist);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم حذف الطبيب/الأخصائي ({name}) بنجاح.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
