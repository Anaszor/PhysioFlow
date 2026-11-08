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
                    TempData["SuccessMessage"] = "تم تحديث بيانات العميل بنجاح!";
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
    }
}
