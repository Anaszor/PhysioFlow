using System.ComponentModel.DataAnnotations;

namespace PhysioClinic.Models
{
    public class Therapist
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الأخصائي مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "المسمى الوظيفي")]
        public string Title { get; set; } = "أخصائي علاج طبيعي وتأهيل";

        [Display(Name = "التخصص الدقيق")]
        public string Specialization { get; set; } = "إصابات رياضية وتأهيل مفاصل";

        [Display(Name = "رقم الهاتف")]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "لون التمييز بالجدول")]
        public string ColorTag { get; set; } = "#0d9488";

        public bool IsActive { get; set; } = true;

        public virtual ICollection<Consultation> Consultations { get; set; } = new List<Consultation>();
        public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
