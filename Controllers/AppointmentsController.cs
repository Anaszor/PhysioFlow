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

        private static List<(string start, string end, string period)> GetStandardTimeslots(int durationMinutes = 60)
        {
            var slots = new List<(string start, string end, string period)>();
            int dur = durationMinutes > 0 ? durationMinutes : 60;

            void AddRange(TimeSpan from, TimeSpan to, string period)
            {
                var cur = from;
                while (cur.Add(TimeSpan.FromMinutes(dur)) <= to)
                {
                    var end = cur.Add(TimeSpan.FromMinutes(dur));
                    slots.Add((cur.ToString(@"hh\:mm"), end.ToString(@"hh\:mm"), period));
                    cur = end;
                }
            }

            AddRange(new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0), "صباحي");
            AddRange(new TimeSpan(13, 0, 0), new TimeSpan(16, 0, 0), "ظهراً");
            AddRange(new TimeSpan(16, 0, 0), new TimeSpan(20, 0, 0), "مسائي");
            return slots;
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

            var activeTherapists = await _context.Therapists.Where(t => t.IsActive).ToListAsync();
            ViewBag.SelectedDate = selectedDate;
            ViewBag.TherapistId = therapistId;
            ViewBag.Therapists = activeTherapists;

            // Generate Timeline Slots for the day (for selected therapist, or first active)
            var timelineTherapistId = therapistId ?? activeTherapists.FirstOrDefault()?.Id ?? 0;
            var rawTimeline = GetStandardTimeslots(60);
            var dayBookings = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .Include(a => a.TreatmentPlan)
                .Where(a => a.AppointmentDate == selectedDate && a.Status != "Cancelled" && (timelineTherapistId == 0 || a.TherapistId == timelineTherapistId))
                .ToListAsync();

            var timelineSlots = rawTimeline.Select(slot =>
            {
                var existing = dayBookings.FirstOrDefault(b => b.StartTime == slot.start);
                return new TimeslotSlot
                {
                    Time = slot.start,
                    EndTime = slot.end,
                    DisplayTime = $"{slot.start} - {slot.end}",
                    Period = slot.period,
                    IsAvailable = existing == null,
                    BookedByClientName = existing?.Client?.FullName,
                    ClientId = existing?.ClientId,
                    ClientPhone = existing?.Client?.Phone,
                    TherapistName = existing?.Therapist?.FullName,
                    AppointmentId = existing?.Id,
                    Status = existing?.Status,
                    SessionType = existing?.TreatmentPlan != null ? existing.TreatmentPlan.Title : "استشارة وتقييم أولي"
                };
            }).ToList();

            ViewBag.TimelineSlots = timelineSlots;
            ViewBag.ActiveClients = await _context.Clients.OrderBy(c => c.FullName).ToListAsync();
            ViewBag.ActivePlans = await _context.TreatmentPlans.Where(p => p.Status == "Active").ToListAsync();

            return View(appointments);
        }

        // GET: Appointments/Book (Receptionist & Admin ONLY)
        [HttpGet]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> Book(int? clientId, int? therapistId, DateTime? date, int duration = 60)
        {
            var selectedDate = date ?? DateTime.Today;
            var therapists = await _context.Therapists.Where(t => t.IsActive).ToListAsync();
            var targetTherapistId = therapistId ?? therapists.FirstOrDefault()?.Id ?? 1;

            var rawTimeslots = GetStandardTimeslots(duration > 0 ? duration : 60);

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
                    EndTime = slot.end,
                    DisplayTime = $"{slot.start} - {slot.end}",
                    Period = slot.period,
                    IsAvailable = existing == null,
                    BookedByClientName = existing?.Client?.FullName,
                    ClientId = existing?.ClientId,
                    AppointmentId = existing?.Id,
                    Status = existing?.Status
                });
            }

            var vm = new BookingViewModel
            {
                ClientId = clientId,
                TherapistId = targetTherapistId,
                SelectedDate = selectedDate,
                DurationMinutes = duration > 0 ? duration : 60,
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
            var effectiveStartTime = !string.IsNullOrWhiteSpace(model.CustomStartTime) ? model.CustomStartTime.Trim() : model.SelectedTime?.Trim();

            if (model.ClientId == null || model.TherapistId == null || string.IsNullOrWhiteSpace(effectiveStartTime))
            {
                ModelState.AddModelError(string.Empty, "يرجى تحديد المريض والمعالج والفترة الزمنية للموعد.");
            }
            else
            {
                // Double-booking check
                var isOccupied = await _context.Appointments
                    .AnyAsync(a => a.TherapistId == model.TherapistId &&
                                   a.AppointmentDate == model.SelectedDate &&
                                   a.StartTime == effectiveStartTime &&
                                   a.Status != "Cancelled");

                if (isOccupied)
                {
                    ModelState.AddModelError(string.Empty, "عذراً، هذه الفترة الزمنية محجوزة مسبقاً لهذا المعالج.");
                }
                else
                {
                    // Compute end time
                    string endTime;
                    if (!string.IsNullOrWhiteSpace(model.CustomEndTime))
                    {
                        endTime = model.CustomEndTime.Trim();
                    }
                    else
                    {
                        var timeParts = effectiveStartTime.Split(':');
                        int startH = int.Parse(timeParts[0]);
                        int startM = int.Parse(timeParts[1]);
                        var startDt = new DateTime(2000, 1, 1, startH, startM, 0);
                        int duration = model.DurationMinutes > 0 ? model.DurationMinutes : 60;
                        var endDt = startDt.AddMinutes(duration);
                        endTime = endDt.ToString("HH:mm");
                    }

                    var appointment = new Appointment
                    {
                        ClientId = model.ClientId.Value,
                        TherapistId = model.TherapistId.Value,
                        TreatmentPlanId = model.TreatmentPlanId,
                        AppointmentDate = model.SelectedDate,
                        StartTime = effectiveStartTime,
                        EndTime = endTime,
                        Status = "Scheduled",
                        SessionNotes = model.Notes,
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
                        TempData["SuccessMessage"] = $"تم تأكيد حجز الموعد بنجاح ({effectiveStartTime} - {endTime}) بتاريخ {model.SelectedDate:yyyy-MM-dd} وأُرسلت رسالة تأكيد واتساب للمريض ({appointment.Client?.FullName}).";
                    }
                    else
                    {
                        TempData["SuccessMessage"] = $"تم تأكيد حجز الموعد بنجاح ({effectiveStartTime} - {endTime}) بتاريخ {model.SelectedDate:yyyy-MM-dd}.";
                    }

                    return RedirectToAction(nameof(Index), new { date = model.SelectedDate.ToString("yyyy-MM-dd"), therapistId = model.TherapistId });
                }
            }

            return await Book(model.ClientId, model.TherapistId, model.SelectedDate, model.DurationMinutes);
        }

        // POST: Appointments/QuickBook (from Index timeline / modal)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> QuickBook(int clientId, int therapistId, DateTime appointmentDate, string startTime, string? endTime, int? treatmentPlanId, string? notes)
        {
            if (clientId <= 0 || therapistId <= 0 || string.IsNullOrWhiteSpace(startTime))
            {
                TempData["ErrorMessage"] = "يرجى تحديد المريض والأخصائي والوقت لحجز الموعد.";
                return RedirectToAction(nameof(Index), new { date = appointmentDate.ToString("yyyy-MM-dd"), therapistId });
            }

            var isOccupied = await _context.Appointments
                .AnyAsync(a => a.TherapistId == therapistId &&
                               a.AppointmentDate == appointmentDate &&
                               a.StartTime == startTime &&
                               a.Status != "Cancelled");

            if (isOccupied)
            {
                TempData["ErrorMessage"] = "عذراً، هذه الفترة الزمنية محجوزة مسبقاً لهذا الأخصائي.";
                return RedirectToAction(nameof(Index), new { date = appointmentDate.ToString("yyyy-MM-dd"), therapistId });
            }

            var calculatedEndTime = endTime;
            if (string.IsNullOrWhiteSpace(calculatedEndTime))
            {
                var timeParts = startTime.Split(':');
                if (timeParts.Length == 2 && int.TryParse(timeParts[0], out int h) && int.TryParse(timeParts[1], out int m))
                {
                    var startDt = new DateTime(2000, 1, 1, h, m, 0);
                    calculatedEndTime = startDt.AddMinutes(60).ToString("HH:mm");
                }
                else
                {
                    calculatedEndTime = startTime;
                }
            }

            var appt = new Appointment
            {
                ClientId = clientId,
                TherapistId = therapistId,
                TreatmentPlanId = treatmentPlanId,
                AppointmentDate = appointmentDate,
                StartTime = startTime,
                EndTime = calculatedEndTime,
                Status = "Scheduled",
                SessionNotes = notes,
                CreatedAt = DateTime.UtcNow
            };

            if (treatmentPlanId.HasValue)
            {
                var plan = await _context.TreatmentPlans.FindAsync(treatmentPlanId.Value);
                if (plan != null)
                {
                    appt.SessionNumber = plan.CompletedSessions + 1;
                }
            }

            _context.Appointments.Add(appt);
            await _context.SaveChangesAsync();

            await _context.Entry(appt).Reference(a => a.Client).LoadAsync();
            await _context.Entry(appt).Reference(a => a.Therapist).LoadAsync();

            try
            {
                await _whatsAppService.SendAppointmentConfirmationAsync(appt);
            }
            catch { }

            TempData["SuccessMessage"] = $"تم حجز الموعد بنجاح للمريض ({appt.Client?.FullName}) في تمام الساعة {startTime}.";
            return RedirectToAction(nameof(Index), new { date = appointmentDate.ToString("yyyy-MM-dd"), therapistId });
        }

        // GET: Appointments/GetTherapistSlots (AJAX API for Timeslot Management)
        [HttpGet]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> GetTherapistSlots(int therapistId, DateTime date, int duration = 60)
        {
            var rawSlots = GetStandardTimeslots(duration > 0 ? duration : 60);
            var bookedAppointments = await _context.Appointments
                .Include(a => a.Client)
                .Where(a => a.TherapistId == therapistId && a.AppointmentDate.Date == date.Date && a.Status != "Cancelled")
                .ToListAsync();

            var result = rawSlots.Select(slot =>
            {
                var existing = bookedAppointments.FirstOrDefault(b => b.StartTime == slot.start);
                return new
                {
                    time = slot.start,
                    endTime = slot.end,
                    displayTime = $"{slot.start} - {slot.end}",
                    period = slot.period,
                    isAvailable = existing == null,
                    bookedBy = existing?.Client?.FullName,
                    clientId = existing?.ClientId,
                    appointmentId = existing?.Id,
                    status = existing?.Status
                };
            });

            return Json(new { success = true, date = date.ToString("yyyy-MM-dd"), therapistId, slots = result });
        }

        // GET: Appointments/GetUpcomingAlerts (Receptionist Live Notification API)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUpcomingAlerts()
        {
            var today = DateTime.Today;
            var nowTime = DateTime.Now.TimeOfDay;

            var appointments = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Therapist)
                .Include(a => a.TreatmentPlan)
                .Where(a => a.AppointmentDate == today && a.Status != "Cancelled" && a.Status != "Completed")
                .ToListAsync();

            var imminentList = new List<object>();
            var waitingList = new List<object>();
            var laterTodayList = new List<object>();

            foreach (var appt in appointments)
            {
                TimeSpan startTimeSpan;
                if (!TimeSpan.TryParse(appt.StartTime, out startTimeSpan))
                {
                    var parts = appt.StartTime.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                        startTimeSpan = new TimeSpan(h, m, 0);
                    else
                        startTimeSpan = TimeSpan.Zero;
                }

                var diffMinutes = (int)(startTimeSpan.TotalMinutes - nowTime.TotalMinutes);

                if (appt.Status == "Confirmed")
                {
                    waitingList.Add(new
                    {
                        id = appt.Id,
                        clientId = appt.ClientId,
                        clientName = appt.Client?.FullName ?? "مريض",
                        phone = appt.Client?.Phone ?? "",
                        therapistName = appt.Therapist?.FullName ?? "",
                        therapistTitle = appt.Therapist?.Title ?? "أخصائي",
                        therapistColor = appt.Therapist?.ColorTag ?? "#0d9488",
                        startTime = appt.StartTime,
                        endTime = appt.EndTime,
                        sessionType = appt.TreatmentPlan?.Title ?? "استشارة وتقييم",
                        status = "Confirmed"
                    });
                }
                else if (appt.Status == "Scheduled")
                {
                    string statusText;
                    bool isImminent = false;

                    if (diffMinutes > 0 && diffMinutes <= 60)
                    {
                        statusText = $"خلال {diffMinutes} دقيقة";
                        isImminent = true;
                    }
                    else if (diffMinutes <= 0 && diffMinutes >= -25)
                    {
                        statusText = diffMinutes == 0 ? "يبدأ الآن!" : $"متأخر {-diffMinutes} دقيقة";
                        isImminent = true;
                    }
                    else if (diffMinutes > 60)
                    {
                        var hours = diffMinutes / 60;
                        var mins = diffMinutes % 60;
                        statusText = hours > 0 ? $"بعد {hours} س و {mins} د" : $"بعد {mins} دقيقة";
                    }
                    else
                    {
                        statusText = "فات موعده";
                    }

                    var item = new
                    {
                        id = appt.Id,
                        clientId = appt.ClientId,
                        clientName = appt.Client?.FullName ?? "مريض",
                        phone = appt.Client?.Phone ?? "",
                        therapistName = appt.Therapist?.FullName ?? "",
                        therapistTitle = appt.Therapist?.Title ?? "أخصائي",
                        therapistColor = appt.Therapist?.ColorTag ?? "#0d9488",
                        startTime = appt.StartTime,
                        endTime = appt.EndTime,
                        sessionType = appt.TreatmentPlan?.Title ?? "استشارة وتقييم",
                        diffMinutes = diffMinutes,
                        statusText = statusText,
                        isImminent = isImminent
                    };

                    if (isImminent)
                    {
                        imminentList.Add(item);
                    }
                    else if (diffMinutes > 60)
                    {
                        laterTodayList.Add(item);
                    }
                }
            }

            return Json(new
            {
                success = true,
                count = imminentList.Count + waitingList.Count,
                imminentCount = imminentList.Count,
                waitingCount = waitingList.Count,
                imminent = imminentList,
                waiting = waitingList,
                upcomingToday = laterTodayList,
                serverTime = DateTime.Now.ToString("HH:mm")
            });
        }

        // POST: Appointments/QuickConfirmAttendance
        [HttpPost]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> QuickConfirmAttendance(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Client)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return Json(new { success = false, message = "الموعد غير موجود." });
            }

            appointment.Status = "Confirmed";
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = appointment.Id,
                clientName = appointment.Client?.FullName,
                message = $"تم تسجيل وصول المريض ({appointment.Client?.FullName}) بالعيادة بنجاح!"
            });
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

        // POST: Appointments/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> Edit(int id, DateTime appointmentDate, string startTime, string endTime, int therapistId, string? notes)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            appointment.AppointmentDate = appointmentDate;
            if (!string.IsNullOrWhiteSpace(startTime))
            {
                appointment.StartTime = startTime;
            }
            if (!string.IsNullOrWhiteSpace(endTime))
            {
                appointment.EndTime = endTime;
            }
            appointment.TherapistId = therapistId;
            if (notes != null)
            {
                appointment.SessionNotes = notes.Trim();
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث بيانات الموعد بنجاح!";
            return RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate });
        }

        // POST: Appointments/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Client)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var date = appointment.AppointmentDate;
            var clientName = appointment.Client?.FullName ?? "المريض";
            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"تم حذف موعد ({clientName}) نهائياً بنجاح.";
            return RedirectToAction(nameof(Index), new { date });
        }
    }
}
