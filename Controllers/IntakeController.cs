using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;
using PhysioClinic.Models.ViewModels;

namespace PhysioClinic.Controllers
{
    [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
    public class IntakeController : Controller
    {
        private readonly PhysioDbContext _context;

        public IntakeController(PhysioDbContext context)
        {
            _context = context;
        }

        // GET: Intake/New
        public async Task<IActionResult> New(int? clientId)
        {
            var vm = new NewIntakeViewModel();

            if (clientId.HasValue)
            {
                var existingClient = await _context.Clients.FindAsync(clientId.Value);
                if (existingClient != null)
                {
                    vm.Client = existingClient;
                    vm.IsNewClient = false;
                    vm.ExistingClientId = existingClient.Id;
                    vm.Intake.ClientId = existingClient.Id;
                }
            }

            ViewBag.ClientsList = await _context.Clients
                .OrderBy(c => c.FullName)
                .Select(c => new { c.Id, c.FullName, c.Phone })
                .ToListAsync();

            return View(vm);
        }

        // POST: Intake/New
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> New(NewIntakeViewModel model)
        {
            if (model.IsNewClient)
            {
                if (string.IsNullOrWhiteSpace(model.Client.FullName) || string.IsNullOrWhiteSpace(model.Client.Phone))
                {
                    ModelState.AddModelError("Client.FullName", "الاسم الكامل ورقم الهاتف مطلوبان لتسجيل عميل جديد");
                }
                else
                {
                    model.Client.CreatedAt = DateTime.UtcNow;
                    _context.Clients.Add(model.Client);
                    await _context.SaveChangesAsync();
                    model.Intake.ClientId = model.Client.Id;
                }
            }
            else
            {
                if (model.ExistingClientId.HasValue)
                {
                    model.Intake.ClientId = model.ExistingClientId.Value;
                }
                else
                {
                    ModelState.AddModelError("ExistingClientId", "يرجى اختيار العميل من القائمة");
                }
            }

            if (string.IsNullOrWhiteSpace(model.Intake.BodyPart))
            {
                ModelState.AddModelError("Intake.BodyPart", "يرجى تحديد موضع الألم على مجسم الجسم أو من القائمة");
            }

            if (ModelState.IsValid)
            {
                model.Intake.CreatedAt = DateTime.UtcNow;
                _context.IntakeForms.Add(model.Intake);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم حفظ نموذج تقييم الإصابة والاستبيان الطبي بنجاح!";
                return RedirectToAction("Details", "Clients", new { id = model.Intake.ClientId });
            }

            ViewBag.ClientsList = await _context.Clients
                .OrderBy(c => c.FullName)
                .Select(c => new { c.Id, c.FullName, c.Phone })
                .ToListAsync();

            return View(model);
        }

        // GET: Intake/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var intake = await _context.IntakeForms
                .Include(i => i.Client)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (intake == null) return NotFound();

            return View(intake);
        }
    }
}
