using System.ComponentModel.DataAnnotations;

namespace PhysioClinic.Models
{
    public class Consultation
    {
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }
        public virtual Client? Client { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الأخصائي المعالج")]
        [Display(Name = "الأخصائي المشرف")]
        public int TherapistId { get; set; }
        public virtual Therapist? Therapist { get; set; }

        [Display(Name = "تاريخ الاستشارة")]
        [DataType(DataType.Date)]
        public DateTime ConsultationDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "الشكوى الرئيسية مطلوبة")]
        [Display(Name = "الشكوى الرئيسية (Chief Complaint)")]
        public string ChiefComplaint { get; set; } = string.Empty;

        [Display(Name = "فحص المدى الحركي (Range of Motion - ROM)")]
        public string? RangeOfMotionFindings { get; set; }

        [Display(Name = "فحص القوة العضلية والاختبارات السريرية (Manual Muscle Testing)")]
        public string? MuscleStrengthTests { get; set; }

        [Required(ErrorMessage = "التشخيص المبدئي مطلوب")]
        [Display(Name = "التشخيص السريري (Clinical Diagnosis)")]
        public string Diagnosis { get; set; } = string.Empty;

        [Display(Name = "أهداف العلاج قصيرة وطويلة المدى (Treatment Goals)")]
        public string? TreatmentGoals { get; set; }

        [Display(Name = "توصيات وملاحظات الأخصائي")]
        public string? ClinicalNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();
    }
}
