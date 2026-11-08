using PhysioClinic.Models;

namespace PhysioClinic.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalClientsCount { get; set; }
        public int TodayAppointmentsCount { get; set; }
        public int ActivePlansCount { get; set; }
        public int CompletedSessionsThisWeek { get; set; }

        public List<Appointment> TodayAppointments { get; set; } = new();
        public List<TreatmentPlan> ActivePlans { get; set; } = new();
        public List<Client> RecentClients { get; set; } = new();
        public List<Therapist> Therapists { get; set; } = new();
    }

    public class NewIntakeViewModel
    {
        public Client Client { get; set; } = new();
        public IntakeForm Intake { get; set; } = new();
        public bool IsNewClient { get; set; } = true;
        public int? ExistingClientId { get; set; }
    }

    public class TimeslotSlot
    {
        public string Time { get; set; } = string.Empty;
        public string DisplayTime { get; set; } = string.Empty;
        public string Period { get; set; } = "صباحي"; // صباحي، مسائي
        public bool IsAvailable { get; set; } = true;
        public string? BookedByClientName { get; set; }
    }

    public class BookingViewModel
    {
        public int? ClientId { get; set; }
        public int? TherapistId { get; set; }
        public int? TreatmentPlanId { get; set; }
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string? SelectedTime { get; set; }

        public List<Client> Clients { get; set; } = new();
        public List<Therapist> Therapists { get; set; } = new();
        public List<TreatmentPlan> ActivePlans { get; set; } = new();
        public List<TimeslotSlot> AvailableSlots { get; set; } = new();
    }

    public class PostSessionFormViewModel
    {
        public int AppointmentId { get; set; }
        public int? TreatmentPlanId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string PlanTitle { get; set; } = string.Empty;
        public int? SessionNumber { get; set; }
        public int? TotalSessions { get; set; }

        public int PainLevelAfter { get; set; } = 3;
        public string? TechniquesUsed { get; set; }
        public string? PatientResponse { get; set; }
        public string? ClinicalNotes { get; set; }
        public string? NextSessionPlan { get; set; }
    }
}
