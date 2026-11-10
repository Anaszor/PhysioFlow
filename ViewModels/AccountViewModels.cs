using System.ComponentModel.DataAnnotations;

namespace PhysioClinic.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى إدخال كلمة المرور")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "تذكرني على هذا الجهاز")]
        public bool RememberMe { get; set; } = true;

        public string? ReturnUrl { get; set; }
    }

    public class UserItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? RoleTitle { get; set; }
        public int? TherapistId { get; set; }
        public string? LinkedTherapist { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, ErrorMessage = "يجب ألا تقل كلمة المرور عن {2} أحرف", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى تحديد الدور الوظيفي")]
        [Display(Name = "الدور الوظيفي")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "المسمى الوظيفي التفصيلي")]
        public string? RoleTitle { get; set; }

        [Display(Name = "ربط بملف أخصائي معالج (في حال كان طبيب/أخصائي)")]
        public int? TherapistId { get; set; }
    }

    public class EditStaffViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "يجب ألا تقل كلمة المرور عن {2} أحرف", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور الجديدة (اتركها فارغة إذا لم ترغب بتغييرها)")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "يرجى تحديد الدور الوظيفي")]
        [Display(Name = "الدور الوظيفي")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "المسمى الوظيفي التفصيلي")]
        public string? RoleTitle { get; set; }

        [Display(Name = "ربط بملف أخصائي معالج (في حال كان طبيب/أخصائي)")]
        public int? TherapistId { get; set; }
    }
}
