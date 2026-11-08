using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models.ViewModels;

namespace PhysioClinic.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly PhysioDbContext _context;

        public DashboardController(PhysioDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var todayAppointments = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .Include(a => a.TreatmentPlan)
                .Where(a => a.AppointmentDate == today)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var activePlans = await _context.TreatmentPlans
                .Include(tp => tp.Client)
                .Include(tp => tp.Therapist)
                .Where(tp => tp.Status == "Active")
                .OrderByDescending(tp => tp.CreatedAt)
                .Take(5)
                .ToListAsync();

            var recentClients = await _context.Clients
                .Include(c => c.IntakeForms)
                .Include(c => c.TreatmentPlans)
                .OrderByDescending(c => c.CreatedAt)
                .Take(6)
                .ToListAsync();

            var therapists = await _context.Therapists
                .Where(t => t.IsActive)
                .ToListAsync();

            var totalClients = await _context.Clients.CountAsync();
            var activePlansCount = await _context.TreatmentPlans.CountAsync(tp => tp.Status == "Active");
            var completedSessionsThisWeek = await _context.Appointments
                .CountAsync(a => a.Status == "Completed" && a.AppointmentDate >= today.AddDays(-7));

            var vm = new DashboardViewModel
            {
                TotalClientsCount = totalClients,
                TodayAppointmentsCount = todayAppointments.Count,
                ActivePlansCount = activePlansCount,
                CompletedSessionsThisWeek = completedSessionsThisWeek,
                TodayAppointments = todayAppointments,
                ActivePlans = activePlans,
                RecentClients = recentClients,
                Therapists = therapists
            };

            return View(vm);
        }
    }
}
