using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;

namespace PhysioClinic.Controllers
{
    [Authorize]
    public class ClientsController : Controller
    {
        private readonly PhysioDbContext _context;

        public ClientsController(PhysioDbContext context)
        {
            _context = context;
        }

        // GET: Clients
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Clients
                .Include(c => c.IntakeForms)
                .Include(c => c.TreatmentPlans)
                .Include(c => c.Appointments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => c.FullName.Contains(search) || 
                                         c.Phone.Contains(search) || 
                                         (c.NationalId != null && c.NationalId.Contains(search)));
                ViewBag.CurrentSearch = search;
            }

            var clients = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(clients);
        }

        // GET: Clients/Details/5 (Full 360-Degree Medical File)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var client = await _context.Clients
                .Include(c => c.IntakeForms.OrderByDescending(i => i.CreatedAt))
                .Include(c => c.Consultations.OrderByDescending(con => con.ConsultationDate))
                    .ThenInclude(con => con.Therapist)
                .Include(c => c.TreatmentPlans.OrderByDescending(tp => tp.CreatedAt))
                    .ThenInclude(tp => tp.Therapist)
                .Include(c => c.Appointments.OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.StartTime))
                    .ThenInclude(a => a.Therapist)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (client == null) return NotFound();

            return View(client);
        }

        // GET: Clients/Create
        public IActionResult Create()
        {
            return View(new Client { DateOfBirth = DateTime.Today.AddYears(-28) });
        }

        // POST: Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,Phone,Email,Gender,DateOfBirth,Occupation,NationalId,EmergencyContact,Notes")] Client client)
        {
            if (ModelState.IsValid)
            {
                client.CreatedAt = DateTime.UtcNow;
                _context.Add(client);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم تسجيل العميل {client.FullName} بنجاح!";
                return RedirectToAction(nameof(Details), new { id = client.Id });
            }
            return View(client);
        }

        // GET: Clients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var client = await _context.Clients.FindAsync(id);
            if (client == null) return NotFound();

            return View(client);
        }

        // POST: Clients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Phone,Email,Gender,DateOfBirth,Occupation,NationalId,EmergencyContact,Notes,CreatedAt")] Client client)
        {
            if (id != client.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(client);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"تم تحديث بيانات المريض ({client.FullName}) بنجاح!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Clients.Any(e => e.Id == client.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Details), new { id = client.Id });
            }
            return View(client);
        }

        // POST: Clients/QuickEdit (Quick AJAX/Form update from appointments table)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickEdit(int id, string fullName, string phone, string? occupation, string? nationalId, string? notes, string? returnUrl)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null) return NotFound();

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone))
            {
                TempData["ErrorMessage"] = "اسم المريض ورقم الهاتف مطلوبان.";
            }
            else
            {
                client.FullName = fullName.Trim();
                client.Phone = phone.Trim();
                if (occupation != null) client.Occupation = occupation.Trim();
                if (nationalId != null) client.NationalId = nationalId.Trim();
                if (notes != null) client.Notes = notes.Trim();

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"تم تحديث بيانات المريض ({client.FullName}) بنجاح!";
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Clients/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var client = await _context.Clients
                .Include(c => c.Appointments)
                .Include(c => c.TreatmentPlans)
                .Include(c => c.IntakeForms)
                .Include(c => c.Consultations)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على ملف المريض المطلوب.";
                return RedirectToAction(nameof(Index));
            }

            var name = client.FullName;
            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم حذف ملف المريض ({name}) وسجلاته المرتبطة بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
