using System.ComponentModel.DataAnnotations;

namespace PhysioClinic.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى تحديد العميل")]
        [Display(Name = "العميل")]
        public int ClientId { get; set; }
        public virtual Client? Client { get; set; }

        [Required(ErrorMessage = "يرجى تحديد الأخصائي")]
        [Display(Name = "الأخصائي")]
        public int TherapistId { get; set; }
        public virtual Therapist? Therapist { get; set; }

        [Display(Name = "الخطة العلاجية")]
        public int? TreatmentPlanId { get; set; }
        public virtual TreatmentPlan? TreatmentPlan { get; set; }

        [Required(ErrorMessage = "تاريخ الموعد مطلوب")]
        [Display(Name = "تاريخ الجلسة")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "وقت البدء مطلوب")]
        [Display(Name = "وقت البدء")]
        public string StartTime { get; set; } = "10:00";

        [Required(ErrorMessage = "وقت الانتهاء مطلوب")]
        [Display(Name = "وقت الانتهاء")]
        public string EndTime { get; set; } = "10:45";

        [Display(Name = "رقم الجلسة")]
        public int? SessionNumber { get; set; }

        [Display(Name = "حالة الموعد")]
        public string Status { get; set; } = "Scheduled"; // Scheduled, Confirmed, Completed, Cancelled, NoShow

        [Display(Name = "ملاحظات الجلسة / تقرير الطبيب")]
        public string? SessionNotes { get; set; }

        [Display(Name = "مقياس الألم بعد الجلسة (VAS 1-10)")]
        [Range(1, 10)]
        public int? PainLevelAfter { get; set; }

        [Display(Name = "الأساليب والتقنيات المنفذة في الجلسة")]
        public string? TechniquesUsed { get; set; }

        [Display(Name = "اسم الطبيب / الأخصائي الموثق للجلسة")]
        public string? CompletedByDoctorName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
