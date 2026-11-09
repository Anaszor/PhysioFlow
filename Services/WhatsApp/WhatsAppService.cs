using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PhysioClinic.Models;

namespace PhysioClinic.Services.WhatsApp
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly WhatsAppSettings _settings;
        private readonly ILogger<WhatsAppService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public WhatsAppService(
            HttpClient httpClient,
            IOptions<WhatsAppSettings> options,
            ILogger<WhatsAppService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;

            var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? "http://localhost:2785" : _settings.ApiUrl.TrimEnd('/');
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
            }

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<WhatsAppSessionStatus> GetSessionStatusAsync()
        {
            var result = new WhatsAppSessionStatus();
            try
            {
                var response = await _httpClient.GetAsync($"/api/sessions/{_settings.SessionId}");
                result.IsServerReachable = true;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var session = JsonSerializer.Deserialize<GatewaySessionResponseDto>(content, _jsonOptions);
                    if (session != null)
                    {
                        result.Status = session.Status ?? "unknown";
                        result.Phone = session.Phone;
                        result.PushName = session.PushName;
                        result.EngineLoaded = session.EngineLoaded;
                        result.ErrorMessage = session.LastError;
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    result.Status = "not_found";
                    result.ErrorMessage = "الجلسة غير مسجلة في البوابة.";
                }
                else
                {
                    result.Status = "error";
                    result.ErrorMessage = $"Gateway responded with status code: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reach WhatsApp Gateway at {ApiUrl}", _settings.ApiUrl);
                result.IsServerReachable = false;
                result.Status = "unreachable";
                result.ErrorMessage = "تعذر الاتصال ببوابة الواتساب. تأكد من تشغيل البوابة على المنفذ 2785.";
            }

            return result;
        }

        public async Task<bool> StartSessionAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"/api/sessions/{_settings.SessionId}/start", null);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("WhatsApp session {SessionId} started successfully.", _settings.SessionId);
                    return true;
                }
                
                var errContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to start WhatsApp session: {StatusCode} {Error}", response.StatusCode, errContent);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting WhatsApp session {SessionId}", _settings.SessionId);
                return false;
            }
        }

        public async Task<WhatsAppPairingResult> RequestPairingCodeAsync(string? phoneNumber = null)
        {
            var phoneToUse = phoneNumber ?? _settings.SenderNumber;
            // Clean to international digits only (e.g. 962786363593)
            var cleanPhone = Regex.Replace(phoneToUse, @"[^\d]", "");
            if (cleanPhone.StartsWith("00"))
            {
                cleanPhone = cleanPhone.Substring(2);
            }
            else if (cleanPhone.StartsWith("07") && cleanPhone.Length == 10)
            {
                cleanPhone = "962" + cleanPhone.Substring(1);
            }

            var result = new WhatsAppPairingResult();

            try
            {
                // Ensure session is started first
                var status = await GetSessionStatusAsync();
                if (!status.IsServerReachable)
                {
                    result.Success = false;
                    result.ErrorMessage = "بوابة الواتساب غير مشغلة حالياً.";
                    return result;
                }

                if (status.Status == "disconnected" || status.Status == "stopped" || status.Status == "failed")
                {
                    await StartSessionAsync();
                    await Task.Delay(2000); // Allow engine to initialize
                }

                var requestBody = JsonSerializer.Serialize(new { phoneNumber = cleanPhone });
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/sessions/{_settings.SessionId}/pairing-code", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var dto = JsonSerializer.Deserialize<GatewayPairingCodeResponseDto>(responseContent, _jsonOptions);
                    result.Success = true;
                    result.PairingCode = dto?.PairingCode;
                    return result;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = $"فشل توليد كود الربط: {response.StatusCode} - {responseContent}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting pairing code for phone {Phone}", cleanPhone);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<string?> GetQrCodeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/sessions/{_settings.SessionId}/qr");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var dto = JsonSerializer.Deserialize<GatewayQrResponseDto>(content, _jsonOptions);
                    return dto?.QrCode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch QR code from gateway");
            }
            return null;
        }

        public async Task<WhatsAppSendResult> SendTextMessageAsync(string recipientPhone, string message)
        {
            var result = new WhatsAppSendResult();

            if (!_settings.Enabled)
            {
                result.Success = false;
                result.ErrorMessage = "إرسال رسائل الواتساب معطل في إعدادات النظام.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(recipientPhone))
            {
                result.Success = false;
                result.ErrorMessage = "رقم هاتف المريض غير متوفر.";
                return result;
            }

            var chatId = NormalizePhoneToChatId(recipientPhone);
            if (string.IsNullOrEmpty(chatId))
            {
                result.Success = false;
                result.ErrorMessage = $"صيغة رقم الهاتف ({recipientPhone}) غير صالحة.";
                return result;
            }

            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    chatId = chatId,
                    text = message
                });

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"/api/sessions/{_settings.SessionId}/messages/send-text", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var dto = JsonSerializer.Deserialize<GatewayMessageResponseDto>(responseContent, _jsonOptions);
                    result.Success = true;
                    result.MessageId = dto?.Id;
                    _logger.LogInformation("WhatsApp message sent to {ChatId}, MsgId: {MessageId}", chatId, dto?.Id);
                    return result;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = $"رمز الخطأ {response.StatusCode}: {responseContent}";
                    _logger.LogWarning("Failed to send WhatsApp message to {ChatId}: {Response}", chatId, responseContent);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending WhatsApp message to {RecipientPhone}", recipientPhone);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        public async Task<bool> SendAppointmentConfirmationAsync(Appointment appointment)
        {
            if (!_settings.AutoSendOnBooking || appointment.Client == null)
            {
                return false;
            }

            var clientName = appointment.Client.FullName;
            var doctorName = appointment.Therapist?.FullName ?? "الأخصائي المعالج";
            var dateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");
            var dayName = GetArabicDayName(appointment.AppointmentDate.DayOfWeek);

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً بك أستاذ/ة {clientName} 🌸");
            sb.AppendLine($"يسعدنا في *{_settings.ClinicName}* تأكيد حجز موعدك بنجاح:");
            sb.AppendLine();
            sb.AppendLine($"📅 *اليوم والتاريخ:* {dayName} {dateStr}");
            sb.AppendLine($"⏰ *الوقت:* من {appointment.StartTime} إلى {appointment.EndTime}");
            sb.AppendLine($"👨‍⚕️ *الأخصائي المتابع:* {doctorName}");
            if (appointment.SessionNumber.HasValue)
            {
                sb.AppendLine($"🔢 *رقم الجلسة:* الجلسة رقم {appointment.SessionNumber}");
            }
            sb.AppendLine();
            sb.AppendLine("📍 *الموقع:* عيادة فيزيو فلو - عمان، الأردن");
            sb.AppendLine($"📞 *لأي تعديل أو استفسار:* {_settings.SenderNumber}");
            sb.AppendLine();
            sb.AppendLine("نتمنى لك دوام الصحة والعافية والشفاء العاجل! ✨");

            var res = await SendTextMessageAsync(appointment.Client.Phone, sb.ToString());
            return res.Success;
        }

        public async Task<bool> SendAppointmentReminderAsync(Appointment appointment)
        {
            if (appointment.Client == null)
            {
                return false;
            }

            var clientName = appointment.Client.FullName;
            var doctorName = appointment.Therapist?.FullName ?? "الأخصائي المعالج";
            var dateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");
            var dayName = GetArabicDayName(appointment.AppointmentDate.DayOfWeek);

            var sb = new StringBuilder();
            sb.AppendLine($"تذكير بموعد جلستك العلاجية ⏰");
            sb.AppendLine($"عزيزنا/عزيزتنا *{clientName}*، نود تذكيركم بموعدكم القادم في *{_settings.ClinicName}*:");
            sb.AppendLine();
            sb.AppendLine($"📅 *التاريخ:* {dayName} {dateStr}");
            sb.AppendLine($"⏰ *الموعد:* الساعة {appointment.StartTime}");
            sb.AppendLine($"👨‍⚕️ *الأخصائي:* {doctorName}");
            sb.AppendLine();
            sb.AppendLine("💡 يرجى التكرم بالحضور قبل الموعد بـ 10 دقائق لضمان راحتكم.");
            sb.AppendLine($"📞 للتواصل أو الاعتذار: {_settings.SenderNumber}");
            sb.AppendLine();
            sb.AppendLine("نسعد دائماً بخدمتكم ورعايتكم 🌿");

            var res = await SendTextMessageAsync(appointment.Client.Phone, sb.ToString());
            return res.Success;
        }

        public async Task<bool> SendAppointmentCancelledAsync(Appointment appointment, string? reason = null)
        {
            if (!_settings.AutoSendOnCancel || appointment.Client == null)
            {
                return false;
            }

            var clientName = appointment.Client.FullName;
            var doctorName = appointment.Therapist?.FullName ?? "الأخصائي المعالج";
            var dateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");

            var sb = new StringBuilder();
            sb.AppendLine($"إشعار بإلغاء الموعد ❌");
            sb.AppendLine($"عزيزنا/عزيزتنا *{clientName}*، نود إبلاغكم بأنه تم إلغاء موعد الجلسة المقررة في *{_settings.ClinicName}*:");
            sb.AppendLine($"📅 التاريخ: {dateStr} الساعة {appointment.StartTime}");
            sb.AppendLine($"👨‍⚕️ المعالج: {doctorName}");
            if (!string.IsNullOrWhiteSpace(reason))
            {
                sb.AppendLine($"📝 السبب: {reason}");
            }
            sb.AppendLine();
            sb.AppendLine($"لإعادة جدولة موعدك في أي وقت، يسعدنا تواصلك معنا مباشرة عبر هذا الرقم: {_settings.SenderNumber}");
            sb.AppendLine("مع تمنياتنا لك بالصحة والعافية دائماً 🌸");

            var res = await SendTextMessageAsync(appointment.Client.Phone, sb.ToString());
            return res.Success;
        }

        public string NormalizePhoneToChatId(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var digits = Regex.Replace(phone, @"[^\d]", "");
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            // Strip leading 00
            if (digits.StartsWith("00"))
            {
                digits = digits.Substring(2);
            }

            // Jordan local number formats:
            // 07XXXXXXXX (10 digits) -> 9627XXXXXXXX
            if (digits.StartsWith("07") && digits.Length == 10)
            {
                digits = "962" + digits.Substring(1);
            }
            // 7XXXXXXXX (9 digits) -> 9627XXXXXXXX
            else if (digits.StartsWith("7") && digits.Length == 9)
            {
                digits = "962" + digits;
            }
            // Saudi local number formats:
            // 05XXXXXXXX (10 digits) -> 9665XXXXXXXX
            else if (digits.StartsWith("05") && digits.Length == 10)
            {
                digits = "966" + digits.Substring(1);
            }

            return $"{digits}@c.us";
        }

        private static string GetArabicDayName(DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Saturday => "السبت",
            DayOfWeek.Sunday => "الأحد",
            DayOfWeek.Monday => "الاثنين",
            DayOfWeek.Tuesday => "الثلاثاء",
            DayOfWeek.Wednesday => "الأربعاء",
            DayOfWeek.Thursday => "الخميس",
            DayOfWeek.Friday => "الجمعة",
            _ => ""
        };
    }
}
