using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhysioClinic.Models
{
    public class TreatmentPlan
    {
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }
        public virtual Client? Client { get; set; }

        [Required]
        public int TherapistId { get; set; }
        public virtual Therapist? Therapist { get; set; }

        public int? ConsultationId { get; set; }
        public virtual Consultation? Consultation { get; set; }

        [Required(ErrorMessage = "عنوان الخطة العلاجية مطلوب")]
        [Display(Name = "مسمى الخطة العلاجية")]
        public string Title { get; set; } = "برنامج إعادة تأهيل مكثف";

        [Required]
        [Range(1, 100, ErrorMessage = "عدد الجلسات يجب أن يكون 1 على الأقل")]
        [Display(Name = "إجمالي الجلسات المقررة")]
        public int TotalSessions { get; set; } = 10;

        [Display(Name = "الجلسات المكتملة")]
        public int CompletedSessions { get; set; } = 0;

        [Display(Name = "التكرار الأسبوعي")]
        public string Frequency { get; set; } = "3 جلسات أسبوعياً";

        [Display(Name = "أساليب العلاج المعتمدة (Modalities)")]
        public string Modalities { get; set; } = "علاج يدوي، تمارين علاجية، تيار كهربائي TENS";

        [Display(Name = "توجيهات الأدوية والكمادات والمكملات المساعدة")]
        public string? MedicationAndHomeCare { get; set; } // كمادات باردة 15 دقيقة، دهان مسكن موضعي، مكمل فيتامين د/مغنيسيوم

        [Display(Name = "برنامج التمارين المنزلية (Home Exercises)")]
        public string? HomeExercises { get; set; }

        [Display(Name = "حالة الخطة")]
        public string Status { get; set; } = "Active"; // Active, Completed, OnHold, Cancelled

        [Display(Name = "تاريخ البدء")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "تاريخ الانتهاء المتوقع")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedEndDate { get; set; } = DateTime.Today.AddDays(30);

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [NotMapped]
        public int ProgressPercentage => TotalSessions > 0 ? (int)Math.Round((double)CompletedSessions / TotalSessions * 100) : 0;
    }
}
