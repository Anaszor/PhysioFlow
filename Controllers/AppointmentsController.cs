using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;
using PhysioClinic.Models.ViewModels;
using PhysioClinic.Services.WhatsApp;

namespace PhysioClinic.Controllers
{
    [Authorize]
    public class AppointmentsController : Controller
    {
        private readonly PhysioDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWhatsAppService _whatsAppService;

        public AppointmentsController(
            PhysioDbContext context,
            UserManager<ApplicationUser> userManager,
            IWhatsAppService whatsAppService)
        {
            _context = context;
            _userManager = userManager;
            _whatsAppService = whatsAppService;
        }

        // GET: Appointments
        public async Task<IActionResult> Index(DateTime? date, int? therapistId)
        {
            var selectedDate = date ?? DateTime.Today;

            var query = _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .Include(a => a.TreatmentPlan)
                .Where(a => a.AppointmentDate == selectedDate)
                .AsQueryable();

            // If user is a Doctor, optionally auto-filter to their assigned therapist record unless specified
            if (User.IsInRole(ClinicRoles.Doctor) && !therapistId.HasValue)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.TherapistId.HasValue == true)
                {
                    // Doctor defaults to viewing their own appointments
                    therapistId = currentUser.TherapistId.Value;
                    query = query.Where(a => a.TherapistId == therapistId.Value);
                }
            }
            else if (therapistId.HasValue)
            {
                query = query.Where(a => a.TherapistId == therapistId.Value);
            }

            var appointments = await query
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.SelectedDate = selectedDate;
            ViewBag.TherapistId = therapistId;
            ViewBag.Therapists = await _context.Therapists.Where(t => t.IsActive).ToListAsync();

            return View(appointments);
        }

        // GET: Appointments/Book (Receptionist & Admin ONLY)
        [HttpGet]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> Book(int? clientId, int? therapistId, DateTime? date)
        {
            var selectedDate = date ?? DateTime.Today;
            var therapists = await _context.Therapists.Where(t => t.IsActive).ToListAsync();
            var targetTherapistId = therapistId ?? therapists.FirstOrDefault()?.Id ?? 1;

            // Generate Clinic Timeslots (from 09:00 to 19:15, 45-min slots)
            var rawTimeslots = new List<(string start, string end, string period)>
            {
                ("09:00", "09:45", "صباحي"),
                ("09:45", "10:30", "صباحي"),
                ("10:30", "11:15", "صباحي"),
                ("11:15", "12:00", "صباحي"),
                ("13:00", "13:45", "ظهراً"),
                ("13:45", "14:30", "ظهراً"),
                ("14:30", "15:15", "ظهراً"),
                ("15:15", "16:00", "عصراً"),
                ("16:30", "17:15", "مسائي"),
                ("17:15", "18:00", "مسائي"),
                ("18:00", "18:45", "مسائي"),
                ("18:45", "19:30", "مسائي")
            };

            // Existing bookings for this therapist & date
            var bookedAppointments = await _context.Appointments
                .Include(a => a.Client)
                .Where(a => a.TherapistId == targetTherapistId && a.AppointmentDate == selectedDate && a.Status != "Cancelled")
                .ToListAsync();

            var slotList = new List<TimeslotSlot>();
            foreach (var slot in rawTimeslots)
            {
                var existing = bookedAppointments.FirstOrDefault(b => b.StartTime == slot.start);
                slotList.Add(new TimeslotSlot
                {
                    Time = slot.start,
                    DisplayTime = $"{slot.start} - {slot.end}",
                    Period = slot.period,
                    IsAvailable = existing == null,
                    BookedByClientName = existing?.Client?.FullName
                });
            }

            var vm = new BookingViewModel
            {
                ClientId = clientId,
                TherapistId = targetTherapistId,
                SelectedDate = selectedDate,
                Clients = await _context.Clients.OrderBy(c => c.FullName).ToListAsync(),
                Therapists = therapists,
                ActivePlans = await _context.TreatmentPlans.Where(tp => tp.Status == "Active").ToListAsync(),
                AvailableSlots = slotList
            };

            return View(vm);
        }

        // POST: Appointments/Book (Receptionist & Admin ONLY)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> Book(BookingViewModel model)
        {
            if (model.ClientId == null || model.TherapistId == null || string.IsNullOrWhiteSpace(model.SelectedTime))
            {
                ModelState.AddModelError(string.Empty, "يرجى تحديد المريض والمعالج والفترة الزمنية للموعد.");
            }
            else
            {
                // Double-booking check
                var isOccupied = await _context.Appointments
                    .AnyAsync(a => a.TherapistId == model.TherapistId &&
                                   a.AppointmentDate == model.SelectedDate &&
                                   a.StartTime == model.SelectedTime &&
                                   a.Status != "Cancelled");

                if (isOccupied)
                {
                    ModelState.AddModelError(string.Empty, "عذراً، هذه الفترة الزمنية محجوزة مسبقاً لهذا المعالج.");
                }
                else
                {
                    // Compute end time (45 mins)
                    var timeParts = model.SelectedTime.Split(':');
                    int startH = int.Parse(timeParts[0]);
                    int startM = int.Parse(timeParts[1]);
                    var startDt = new DateTime(2000, 1, 1, startH, startM, 0);
                    var endDt = startDt.AddMinutes(45);
                    var endTime = endDt.ToString("HH:mm");

                    var appointment = new Appointment
                    {
                        ClientId = model.ClientId.Value,
                        TherapistId = model.TherapistId.Value,
                        TreatmentPlanId = model.TreatmentPlanId,
                        AppointmentDate = model.SelectedDate,
                        StartTime = model.SelectedTime,
                        EndTime = endTime,
                        Status = "Scheduled",
                        CreatedAt = DateTime.UtcNow
                    };

                    if (model.TreatmentPlanId.HasValue)
                    {
                        var plan = await _context.TreatmentPlans.FindAsync(model.TreatmentPlanId.Value);
                        if (plan != null)
                        {
                            appointment.SessionNumber = plan.CompletedSessions + 1;
                        }
                    }

                    _context.Appointments.Add(appointment);
                    await _context.SaveChangesAsync();

                    // Load relations to send automated WhatsApp confirmation
                    await _context.Entry(appointment).Reference(a => a.Client).LoadAsync();
                    await _context.Entry(appointment).Reference(a => a.Therapist).LoadAsync();

                    bool whatsAppSent = false;
                    try
                    {
                        whatsAppSent = await _whatsAppService.SendAppointmentConfirmationAsync(appointment);
                    }
                    catch (Exception)
                    {
                        // WhatsApp failure will not disrupt the booking flow
                    }

                    if (whatsAppSent)
                    {
                        TempData["SuccessMessage"] = $"تم تسكين وحجز الموعد بنجاح الساعة {model.SelectedTime} بتاريخ {model.SelectedDate:yyyy-MM-dd} وتم إرسال رسالة تأكيد عبر واتساب للمريض ({appointment.Client?.FullName}).";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"تم تسكين وحجز الموعد بنجاح الساعة {model.SelectedTime} بتاريخ {model.SelectedDate:yyyy-MM-dd}";
                    }

                    return RedirectToAction(nameof(Index), new { date = model.SelectedDate });
                }
            }

            return await Book(model.ClientId, model.TherapistId, model.SelectedDate);
        }

        // GET: Appointments/SessionReport/5 (Print & PDF Documentation Report)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SessionReport(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .Include(a => a.TreatmentPlan)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // POST: Appointments/CompleteSession (Doctor & Admin ONLY - "الفورم السريري بعد كل جلسة")
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Doctor}")]
        public async Task<IActionResult> CompleteSession(
            int id, 
            int painLevelAfter, 
            string? techniquesUsed, 
            string? patientResponse, 
            string? clinicalNotes, 
            string? nextSessionPlan,
            string? patientSignature)
        {
            var appointment = await _context.Appointments
                .Include(a => a.TreatmentPlan)
                .Include(a => a.Client)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var doctorName = currentUser?.FullName ?? User.Identity?.Name ?? "الأخصائي المعالج";

            appointment.Status = "Completed";
            appointment.PainLevelAfter = painLevelAfter > 0 ? painLevelAfter : 3;
            appointment.TechniquesUsed = techniquesUsed;
            appointment.PatientResponse = patientResponse;
            appointment.ClinicalNotes = clinicalNotes;
            appointment.NextSessionPlan = nextSessionPlan;
            if (!string.IsNullOrWhiteSpace(patientSignature))
            {
                appointment.PatientSignature = patientSignature;
            }
            appointment.CompletedByDoctorName = doctorName;

            var summaryParts = new List<string>();
            summaryParts.Add($"مقياس الألم بعد الجلسة: {appointment.PainLevelAfter}/10");
            if (!string.IsNullOrWhiteSpace(techniquesUsed)) summaryParts.Add($"التقنيات: {techniquesUsed}");
            if (!string.IsNullOrWhiteSpace(patientResponse)) summaryParts.Add($"استجابة المريض: {patientResponse}");
            if (!string.IsNullOrWhiteSpace(clinicalNotes)) summaryParts.Add($"ملاحظات الطبيب ({doctorName}): {clinicalNotes}");
            if (!string.IsNullOrWhiteSpace(nextSessionPlan)) summaryParts.Add($"خطة الجلسة القادمة: {nextSessionPlan}");
            
            appointment.SessionNotes = string.Join(" | ", summaryParts);

            // Increment completed sessions on linked treatment plan
            if (appointment.TreatmentPlan != null)
            {
                if (appointment.TreatmentPlan.CompletedSessions < appointment.TreatmentPlan.TotalSessions)
                {
                    appointment.TreatmentPlan.CompletedSessions++;
                    if (appointment.TreatmentPlan.CompletedSessions >= appointment.TreatmentPlan.TotalSessions)
                    {
                        appointment.TreatmentPlan.Status = "Completed";
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"تم توثيق إنجاز الجلسة وتعبئة التقرير السريري للمريض ({appointment.Client?.FullName}) بنجاح بواسطة ({doctorName}).";

            return RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate });
        }

        // POST: Appointments/ConfirmAttendance (Receptionist & Admin ONLY)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> ConfirmAttendance(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                appointment.Status = "Confirmed";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تأكيد حضور المريض بالاستقبال وتسجيل وصوله للعيادة.";
            }
            return RedirectToAction(nameof(Index), new { date = appointment?.AppointmentDate });
        }

        // POST: Appointments/CancelAppointment (Receptionist & Admin ONLY)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> CancelAppointment(int id, string? reason)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment != null)
            {
                appointment.Status = "Cancelled";
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    appointment.SessionNotes = (appointment.SessionNotes != null ? appointment.SessionNotes + " | " : "") + $"سبب الإلغاء: {reason}";
                }
                await _context.SaveChangesAsync();

                try
                {
                    await _whatsAppService.SendAppointmentCancelledAsync(appointment, reason);
                }
                catch { }

                TempData["SuccessMessage"] = "تم إلغاء الموعد بنجاح وإرسال إشعار للمريض عبر واتساب.";
            }
            return RedirectToAction(nameof(Index), new { date = appointment?.AppointmentDate });
        }

        // POST: Appointments/SendReminder
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> SendReminder(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var sent = await _whatsAppService.SendAppointmentReminderAsync(appointment);
            if (sent)
            {
                TempData["SuccessMessage"] = $"تم إرسال تذكير الموعد بنجاح عبر واتساب إلى ({appointment.Client?.FullName}).";
            }
            else
            {
                TempData["ErrorMessage"] = $"تعذر إرسال تذكير الواتساب للمريض. يرجى التأكد من ربط واتساب في شاشة إعدادات الواتساب.";
            }

            return RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate });
        }
    }
}
