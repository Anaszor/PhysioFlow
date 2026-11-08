namespace PhysioClinic.Models
{
    public static class ClinicRoles
    {
        public const string Admin = "Admin";
        public const string Doctor = "Doctor";
        public const string Receptionist = "Receptionist";

        public static readonly string[] All = { Admin, Doctor, Receptionist };

        public static string GetArabicTitle(string role) => role switch
        {
            Admin => "مدير النظام (Admin)",
            Doctor => "أخصائي / طبيب معالج (Doctor)",
            Receptionist => "موظف استقبال (Receptionist)",
            _ => role
        };

        public static string GetBadgeClass(string role) => role switch
        {
            Admin => "bg-purple-subtle text-purple border",
            Doctor => "bg-teal-subtle text-teal border",
            Receptionist => "bg-info-subtle text-info border",
            _ => "bg-light text-secondary border"
        };
    }
}
