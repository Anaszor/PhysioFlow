using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhysioClinic.Models
{
    public class Client
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        [Display(Name = "رقم الهاتف")]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Display(Name = "الجنس")]
        public string Gender { get; set; } = "Male"; // Male, Female

        [Display(Name = "تاريخ الميلاد")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-30);

        [Display(Name = "المهنة / طبيعة العمل")]
        public string? Occupation { get; set; } // e.g. مهندس برمجيات، رياضي، معلم

        [Display(Name = "رقم الهوية / الإقامة")]
        public string? NationalId { get; set; }

        [Display(Name = "جهة اتصال الطوارئ")]
        public string? EmergencyContact { get; set; }

        [Display(Name = "ملاحظات إضافية")]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ التسجيل")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<IntakeForm> IntakeForms { get; set; } = new List<IntakeForm>();
        public virtual ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
        public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        [NotMapped]
        public int Age => DateTime.Today.Year - DateOfBirth.Year - (DateTime.Today.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);
    }
}
