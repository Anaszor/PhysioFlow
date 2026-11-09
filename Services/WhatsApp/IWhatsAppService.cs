using PhysioClinic.Models;

namespace PhysioClinic.Services.WhatsApp
{
    public interface IWhatsAppService
    {
        Task<WhatsAppSessionStatus> GetSessionStatusAsync();
        Task<bool> StartSessionAsync();
        Task<WhatsAppPairingResult> RequestPairingCodeAsync(string? phoneNumber = null);
        Task<string?> GetQrCodeAsync();
        Task<WhatsAppSendResult> SendTextMessageAsync(string recipientPhone, string message);
        Task<bool> SendAppointmentConfirmationAsync(Appointment appointment);
        Task<bool> SendAppointmentReminderAsync(Appointment appointment);
        Task<bool> SendAppointmentCancelledAsync(Appointment appointment, string? reason = null);
        string NormalizePhoneToChatId(string phone);
    }
}
