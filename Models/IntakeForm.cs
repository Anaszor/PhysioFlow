using System.ComponentModel.DataAnnotations;

namespace PhysioClinic.Models
{
    public class IntakeForm
    {
        public int Id { get; set; }

        [Required]
        public int ClientId { get; set; }
        public virtual Client? Client { get; set; }

        [Required(ErrorMessage = "يرجى تحديد موضع الألم أو الإصابة")]
        [Display(Name = "موضع الإصابة")]
        public string BodyPart { get; set; } = string.Empty; // e.g., "الرقبة", "أسفل الظهر", "الكتف الأيمن", "الركبة اليسرى"

        [Required(ErrorMessage = "نوع الإصابة مطلوب")]
        [Display(Name = "نوع الإصابة المتوقعة")]
        public string InjuryType { get; set; } = "ألم حاد مفاجئ"; // تمزق عضلي، ديسك/انزلاق غضروفي، التهاب أوتار، تيبس مفاصل، ما بعد جراحة

        [Display(Name = "سبب الإصابة")]
        public string? InjuryCause { get; set; } // نشاط رياضي، حادث سير، حركة خاطئة، الجلوس المكتبي الطويل

        [Display(Name = "تاريخ بداية الألم")]
        [DataType(DataType.Date)]
        public DateTime OnsetDate { get; set; } = DateTime.Today;

        [Range(1, 10, ErrorMessage = "مقياس الألم يجب أن يكون بين 1 و 10")]
        [Display(Name = "شدة الألم (VAS Scale 1-10)")]
        public int PainLevel { get; set; } = 5;

        [Display(Name = "طبيعة الألم وصفته")]
        public string? PainDescription { get; set; } // حاد، نابض، حارق، متقطع، مستمر

        [Display(Name = "الأعراض المصاحبة")]
        public string? Symptoms { get; set; } // تنميل، وخز، ضعف عضلات، صعوبة بالمشي، تورم

        [Display(Name = "ما الذي يزيد الألم؟")]
        public string? AggravatingFactors { get; set; } // المشي، الجلوس الطويل، الانحناء

        [Display(Name = "ما الذي يخفف الألم؟")]
        public string? RelievingFactors { get; set; } // الراحة، الكمادات الساخنة، المسكنات

        [Display(Name = "التاريخ المرضي المسبق")]
        public string? MedicalHistoryFlags { get; set; } // ضغط، سكري، عمليات جراحية سابقة، شاشية عظام

        [Display(Name = "هل تم عمل صور إشعاعية (أشعة / رنين)؟")]
        public bool HasRadiologyReports { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
