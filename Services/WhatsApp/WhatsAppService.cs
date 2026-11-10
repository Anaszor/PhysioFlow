using Microsoft.Extensions.Options;
using PhysioClinic.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<WhatsAppSessionStatus> GetSessionStatusAsync()
        {
            var result = new WhatsAppSessionStatus();

            if (string.Equals(_settings.Provider, "GreenApi", StringComparison.OrdinalIgnoreCase))
            {
                return await GetGreenApiStatusAsync();
            }
            else if (string.Equals(_settings.Provider, "UltraMsg", StringComparison.OrdinalIgnoreCase))
            {
                return await GetUltraMsgStatusAsync();
            }
            else
            {
                return await GetGatewayStatusAsync();
            }
        }

        private async Task<WhatsAppSessionStatus> GetGreenApiStatusAsync()
        {
            var result = new WhatsAppSessionStatus();
            if (string.IsNullOrWhiteSpace(_settings.InstanceId) || string.IsNullOrWhiteSpace(_settings.ApiToken))
            {
                result.IsServerReachable = true;
                result.Status = "not_configured";
                result.ErrorMessage = "يرجى إدخال معرّف الحساب (IdInstance) والرمز (ApiToken) في الإعدادات.";
                return result;
            }

            try
            {
                var url = $"https://api.green-api.com/waInstance{_settings.InstanceId}/getStateInstance/{_settings.ApiToken}";
                var response = await _httpClient.GetAsync(url);
                result.IsServerReachable = response.IsSuccessStatusCode;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    var state = doc.RootElement.TryGetProperty("stateInstance", out var s) ? s.GetString() : "unknown";
                    result.Status = state ?? "unknown";

                    if (state == "authorized")
                    {
                        result.Phone = _settings.SenderNumber;
                        result.PushName = _settings.ClinicName;
                        result.EngineLoaded = true;
                    }
                    else if (state == "notAuthorized")
                    {
                        result.Status = "qr_ready";
                        result.ErrorMessage = "بانتظار مسح رمز QR من هاتفك لربط الرقم.";
                    }
                }
                else
                {
                    result.Status = "error";
                    result.ErrorMessage = $"استجابة المزود السحابي: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reach Green-API endpoint");
                result.IsServerReachable = false;
                result.Status = "unreachable";
                result.ErrorMessage = "تعذر الاتصال بالمزود السحابي (Green-API).";
            }

            return result;
        }

        private async Task<WhatsAppSessionStatus> GetUltraMsgStatusAsync()
        {
            var result = new WhatsAppSessionStatus();
            if (string.IsNullOrWhiteSpace(_settings.InstanceId) || string.IsNullOrWhiteSpace(_settings.ApiToken))
            {
                result.IsServerReachable = true;
                result.Status = "not_configured";
                result.ErrorMessage = "يرجى إدخال Instance ID و Token الخاص بـ UltraMsg في الإعدادات.";
                return result;
            }

            try
            {
                var url = $"https://api.ultramsg.com/{_settings.InstanceId}/instance/status?token={_settings.ApiToken}";
                var response = await _httpClient.GetAsync(url);
                result.IsServerReachable = response.IsSuccessStatusCode;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("status", out var statusObj) &&
                        statusObj.TryGetProperty("accountStatus", out var accStatus) &&
                        accStatus.TryGetProperty("status", out var st))
                    {
                        var statusStr = st.GetString();
                        if (statusStr == "authenticated")
                        {
                            result.Status = "ready";
                            result.Phone = _settings.SenderNumber;
                            result.PushName = _settings.ClinicName;
                            result.EngineLoaded = true;
                        }
                        else
                        {
                            result.Status = "qr_ready";
                        }
                    }
                }
                else
                {
                    result.Status = "error";
                    result.ErrorMessage = $"استجابة UltraMsg: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reach UltraMsg endpoint");
                result.IsServerReachable = false;
                result.Status = "unreachable";
                result.ErrorMessage = "تعذر الاتصال بخادم UltraMsg.";
            }

            return result;
        }

        private async Task<WhatsAppSessionStatus> GetGatewayStatusAsync()
        {
            var result = new WhatsAppSessionStatus();
            var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? "http://localhost:2785" : _settings.ApiUrl.TrimEnd('/');

            try
            {
                HttpResponseMessage? response = null;
                if (!string.IsNullOrWhiteSpace(_settings.SessionId) && _settings.SessionId.Length == 36)
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/sessions/{_settings.SessionId}");
                    if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                    {
                        request.Headers.Add("x-api-key", _settings.ApiKey);
                    }
                    response = await _httpClient.SendAsync(request);
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    // Fall back to auto-discovering sessions on Gateway
                    var listRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/sessions");
                    if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                    {
                        listRequest.Headers.Add("x-api-key", _settings.ApiKey);
                    }
                    var listResponse = await _httpClient.SendAsync(listRequest);
                    result.IsServerReachable = listResponse.IsSuccessStatusCode;

                    if (listResponse.IsSuccessStatusCode)
                    {
                        var listContent = await listResponse.Content.ReadAsStringAsync();
                        var sessions = JsonSerializer.Deserialize<List<GatewaySessionResponseDto>>(listContent, _jsonOptions);
                        var active = sessions?.FirstOrDefault(s => s.Status == "ready" || s.Status == "qr_ready") ?? sessions?.FirstOrDefault();

                        if (active != null && !string.IsNullOrEmpty(active.Id))
                        {
                            _settings.InstanceId = active.Id;
                            result.Status = active.Status ?? "unknown";
                            result.Phone = active.Phone;
                            result.PushName = active.PushName;
                            result.EngineLoaded = active.EngineLoaded;
                            result.ErrorMessage = active.LastError;

                            if (result.Status == "created" || result.Status == "stopped")
                            {
                                await StartSessionAsync();
                                result.Status = "qr_ready";
                            }
                            return result;
                        }
                        else
                        {
                            // Auto-create session
                            var createReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/sessions")
                            {
                                Content = new StringContent(JsonSerializer.Serialize(new { name = "clinic" }), Encoding.UTF8, "application/json")
                            };
                            if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) createReq.Headers.Add("x-api-key", _settings.ApiKey);
                            var createResp = await _httpClient.SendAsync(createReq);
                            if (createResp.IsSuccessStatusCode)
                            {
                                var createContent = await createResp.Content.ReadAsStringAsync();
                                var newSess = JsonSerializer.Deserialize<GatewaySessionResponseDto>(createContent, _jsonOptions);
                                if (newSess != null && !string.IsNullOrEmpty(newSess.Id))
                                {
                                    _settings.InstanceId = newSess.Id;
                                    await StartSessionAsync();
                                    result.Status = "qr_ready";
                                    return result;
                                }
                            }
                            result.Status = "not_found";
                            result.ErrorMessage = "لا توجد جلسات مسجلة في البوابة.";
                            return result;
                        }
                    }
                    else if (listResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        result.Status = "error";
                        result.ErrorMessage = "مفتاح API غير صالح للاتصال ببوابة الواتساب (401 Unauthorized).";
                        return result;
                    }
                    else
                    {
                        result.Status = "error";
                        result.ErrorMessage = $"Gateway responded with status code: {listResponse.StatusCode}";
                        return result;
                    }
                }
                else
                {
                    result.IsServerReachable = true;
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
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not reach WhatsApp Gateway at {ApiUrl}", baseUrl);
                result.IsServerReachable = false;
                result.Status = "unreachable";
                result.ErrorMessage = "تعذر الاتصال ببوابة الواتساب.";
            }

            return result;
        }

        public async Task<bool> StartSessionAsync()
        {
            if (string.Equals(_settings.Provider, "GreenApi", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_settings.Provider, "UltraMsg", StringComparison.OrdinalIgnoreCase))
            {
                // Cloud providers manage sessions automatically 24/7
                return true;
            }

            try
            {
                var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? "http://localhost:2785" : _settings.ApiUrl.TrimEnd('/');
                var sessionId = _settings.SessionId;
                if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length != 36)
                {
                    var listRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/sessions");
                    if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) listRequest.Headers.Add("x-api-key", _settings.ApiKey);
                    var listResp = await _httpClient.SendAsync(listRequest);
                    if (listResp.IsSuccessStatusCode)
                    {
                        var listContent = await listResp.Content.ReadAsStringAsync();
                        var sessions = JsonSerializer.Deserialize<List<GatewaySessionResponseDto>>(listContent, _jsonOptions);
                        var active = sessions?.FirstOrDefault();
                        if (active != null && !string.IsNullOrEmpty(active.Id))
                        {
                            sessionId = active.Id;
                            _settings.InstanceId = active.Id;
                        }
                    }
                }

                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/sessions/{sessionId}/start")
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
                if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                {
                    request.Headers.Add("x-api-key", _settings.ApiKey);
                }

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting WhatsApp gateway session {SessionId}", _settings.SessionId);
                return false;
            }
        }

        public async Task<WhatsAppPairingResult> RequestPairingCodeAsync(string? phoneNumber = null)
        {
            var phoneToUse = phoneNumber ?? _settings.SenderNumber;
            var cleanDigits = NormalizePhoneDigits(phoneToUse);

            var result = new WhatsAppPairingResult();

            if (string.Equals(_settings.Provider, "GreenApi", StringComparison.OrdinalIgnoreCase))
            {
                // Green-API uses QR code
                var qr = await GetQrCodeAsync();
                result.Success = !string.IsNullOrEmpty(qr);
                result.QrCodeBase64 = qr;
                result.ErrorMessage = result.Success ? null : "يرجى مسح رمز الـ QR من صفحة Green-API.";
                return result;
            }

            // Gateway pairing code
            try
            {
                var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? "http://localhost:2785" : _settings.ApiUrl.TrimEnd('/');
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/sessions/{_settings.SessionId}/pairing-code")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { phoneNumber = cleanDigits }), Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                {
                    request.Headers.Add("x-api-key", _settings.ApiKey);
                }

                var response = await _httpClient.SendAsync(request);
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
                _logger.LogError(ex, "Error requesting pairing code for phone {Phone}", cleanDigits);
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<string?> GetQrCodeAsync()
        {
            try
            {
                if (string.Equals(_settings.Provider, "GreenApi", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(_settings.InstanceId) || string.IsNullOrWhiteSpace(_settings.ApiToken))
                        return null;

                    var url = $"https://api.green-api.com/waInstance{_settings.InstanceId}/qr/{_settings.ApiToken}";
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("message", out var msg))
                        {
                            var b64 = msg.GetString();
                            if (!string.IsNullOrEmpty(b64) && !b64.StartsWith("data:image"))
                            {
                                return $"data:image/png;base64,{b64}";
                            }
                            return b64;
                        }
                    }
                    return null;
                }
                else if (string.Equals(_settings.Provider, "UltraMsg", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(_settings.InstanceId) || string.IsNullOrWhiteSpace(_settings.ApiToken))
                        return null;

                    var url = $"https://api.ultramsg.com/{_settings.InstanceId}/instance/qr?token={_settings.ApiToken}";
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("qr", out var qrProp))
                        {
                            var b64 = qrProp.GetString();
                            if (!string.IsNullOrEmpty(b64) && !b64.StartsWith("data:image"))
                            {
                                return $"data:image/png;base64,{b64}";
                            }
                            return b64;
                        }
                    }
                    return null;
                }
                else
                {
                    var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? "http://localhost:2785" : _settings.ApiUrl.TrimEnd('/');
                    var sessionId = _settings.SessionId;
                    if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length != 36)
                    {
                        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/sessions");
                        if (!string.IsNullOrWhiteSpace(_settings.ApiKey)) listRequest.Headers.Add("x-api-key", _settings.ApiKey);
                        var listResp = await _httpClient.SendAsync(listRequest);
                        if (listResp.IsSuccessStatusCode)
                        {
                            var listContent = await listResp.Content.ReadAsStringAsync();
                            var sessions = JsonSerializer.Deserialize<List<GatewaySessionResponseDto>>(listContent, _jsonOptions);
                            var active = sessions?.FirstOrDefault(s => s.Status == "qr_ready" || s.Status == "ready") ?? sessions?.FirstOrDefault();
                            if (active != null && !string.IsNullOrEmpty(active.Id))
                            {
                                sessionId = active.Id;
                                _settings.InstanceId = active.Id;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/sessions/{sessionId}/qr");
                        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                        {
                            request.Headers.Add("x-api-key", _settings.ApiKey);
                        }

                        var response = await _httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var dto = JsonSerializer.Deserialize<GatewayQrResponseDto>(content, _jsonOptions);
                            return dto?.QrCode;
                        }
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve QR code");
                return null;
            }
        }

        public async Task<WhatsAppSendResult> SendTextMessageAsync(string recipientPhone, string message)
        {
            var result = new WhatsAppSendResult();

            if (!_settings.Enabled)
            {
                result.Success = false;
                result.ErrorMessage = "خدمة الواتساب معطلة حالياً في الإعدادات.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(recipientPhone))
            {
                result.Success = false;
                result.ErrorMessage = "رقم هاتف المريض غير متوفر.";
                return result;
            }

            var cleanDigits = NormalizePhoneDigits(recipientPhone);
            if (string.IsNullOrEmpty(cleanDigits))
            {
                result.Success = false;
                result.ErrorMessage = $"صيغة رقم الهاتف ({recipientPhone}) غير صالحة.";
                return result;
            }

            try
            {
                if (string.Equals(_settings.Provider, "GreenApi", StringComparison.OrdinalIgnoreCase))
                {
                    var url = $"https://api.green-api.com/waInstance{_settings.InstanceId}/sendMessage/{_settings.ApiToken}";
                    var payload = JsonSerializer.Serialize(new
                    {
                        chatId = $"{cleanDigits}@c.us",
                        message = message
                    });

                    var response = await _httpClient.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"));
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        var id = doc.RootElement.TryGetProperty("idMessage", out var idProp) ? idProp.GetString() : "ok";
                        result.Success = true;
                        result.MessageId = id;
                        _logger.LogInformation("WhatsApp message sent via Green-API to {Phone}, MsgId: {Id}", cleanDigits, id);
                        return result;
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Green-API ({response.StatusCode}): {responseContent}";
                        _logger.LogWarning("Failed Green-API send to {Phone}: {Err}", cleanDigits, responseContent);
                        return result;
                    }
                }
                else if (string.Equals(_settings.Provider, "UltraMsg", StringComparison.OrdinalIgnoreCase))
                {
                    var url = $"https://api.ultramsg.com/{_settings.InstanceId}/messages/chat";
                    var payload = JsonSerializer.Serialize(new
                    {
                        token = _settings.ApiToken,
                        to = cleanDigits,
                        body = message
                    });

                    var response = await _httpClient.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"));
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        result.Success = true;
                        result.MessageId = "ok";
                        _logger.LogInformation("WhatsApp message sent via UltraMsg to {Phone}", cleanDigits);
                        return result;
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"UltraMsg ({response.StatusCode}): {responseContent}";
                        return result;
                    }
                }
                else
                {
                    // Gateway fallback
                    var baseUrl = string.IsNullOrWhiteSpace(_settings.ApiUrl) ? "http://localhost:2785" : _settings.ApiUrl.TrimEnd('/');
                    var payload = JsonSerializer.Serialize(new
                    {
                        chatId = $"{cleanDigits}@c.us",
                        text = message
                    });

                    var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/sessions/{_settings.SessionId}/messages/send-text")
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };

                    if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                    {
                        request.Headers.Add("x-api-key", _settings.ApiKey);
                    }

                    var response = await _httpClient.SendAsync(request);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var dto = JsonSerializer.Deserialize<GatewayMessageResponseDto>(responseContent, _jsonOptions);
                        result.Success = true;
                        result.MessageId = dto?.Id;
                        _logger.LogInformation("WhatsApp message sent via Gateway to {Phone}, MsgId: {MessageId}", cleanDigits, dto?.Id);
                        return result;
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"رمز الخطأ {response.StatusCode}: {responseContent}";
                        return result;
                    }
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

            var text = BuildConfirmationMessage(appointment);
            var res = await SendTextMessageAsync(appointment.Client.Phone, text);
            return res.Success;
        }

        public async Task<bool> SendAppointmentReminderAsync(Appointment appointment)
        {
            if (appointment.Client == null)
            {
                return false;
            }

            var text = BuildReminderMessage(appointment);
            var res = await SendTextMessageAsync(appointment.Client.Phone, text);
            return res.Success;
        }

        public async Task<bool> SendAppointmentCancelledAsync(Appointment appointment, string? reason = null)
        {
            if (!_settings.AutoSendOnCancel || appointment.Client == null)
            {
                return false;
            }

            var clientName = appointment.Client.FullName;
            var dateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");
            var dayName = GetArabicDayName(appointment.AppointmentDate.DayOfWeek);

            var sb = new StringBuilder();
            sb.AppendLine($"مرحباً بك أستاذ/ة {clientName} 🌸");
            sb.AppendLine($"نود إعلامك بأنه تم *إلغاء الموعد* المحجوز لدى *{_settings.ClinicName}*:");
            sb.AppendLine($"📅 الموعد: {dayName} {dateStr} (من {appointment.StartTime} إلى {appointment.EndTime})");

            if (!string.IsNullOrWhiteSpace(reason))
            {
                sb.AppendLine($"📝 سبب الإلغاء: {reason}");
            }

            sb.AppendLine();
            sb.AppendLine($"لإعادة الجدولة أو حجز موعد جديد في أي وقت، يمكنك التواصل معنا عبر هذا الرقم: {_settings.SenderNumber}");
            sb.AppendLine("نسعد دائماً بخدمتك ونتمنى لك دوام الصحة والعافية! 🌿");

            var res = await SendTextMessageAsync(appointment.Client.Phone, sb.ToString());
            return res.Success;
        }

        public string NormalizePhoneToChatId(string phone)
        {
            var digits = NormalizePhoneDigits(phone);
            return string.IsNullOrEmpty(digits) ? string.Empty : $"{digits}@c.us";
        }

        public string NormalizePhoneDigits(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var digits = Regex.Replace(phone, @"[^\d]", "");
            if (string.IsNullOrWhiteSpace(digits))
                return string.Empty;

            if (digits.StartsWith("00"))
            {
                digits = digits.Substring(2);
            }

            // Jordan: 07XXXXXXXX (10) -> 9627XXXXXXXX
            if (digits.StartsWith("07") && digits.Length == 10)
            {
                digits = "962" + digits.Substring(1);
            }
            // Jordan: 7XXXXXXXX (9) -> 9627XXXXXXXX
            else if (digits.StartsWith("7") && digits.Length == 9)
            {
                digits = "962" + digits;
            }
            // Saudi: 05XXXXXXXX (10) -> 9665XXXXXXXX
            else if (digits.StartsWith("05") && digits.Length == 10)
            {
                digits = "966" + digits.Substring(1);
            }

            return digits;
        }

        public string GetDirectWhatsAppUrl(string phone, string message)
        {
            var digits = NormalizePhoneDigits(phone);
            if (string.IsNullOrEmpty(digits)) return "#";
            return $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
        }

        public string GetAppointmentConfirmationWhatsAppUrl(Appointment appointment)
        {
            if (appointment.Client == null) return "#";
            var text = BuildConfirmationMessage(appointment);
            return GetDirectWhatsAppUrl(appointment.Client.Phone, text);
        }

        public string GetAppointmentReminderWhatsAppUrl(Appointment appointment)
        {
            if (appointment.Client == null) return "#";
            var text = BuildReminderMessage(appointment);
            return GetDirectWhatsAppUrl(appointment.Client.Phone, text);
        }

        private string BuildConfirmationMessage(Appointment appointment)
        {
            var clientName = appointment.Client?.FullName ?? "العميل الكريم";
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

            return sb.ToString();
        }

        private string BuildReminderMessage(Appointment appointment)
        {
            var clientName = appointment.Client?.FullName ?? "العميل الكريم";
            var doctorName = appointment.Therapist?.FullName ?? "الأخصائي المعالج";
            var dateStr = appointment.AppointmentDate.ToString("yyyy-MM-dd");
            var dayName = GetArabicDayName(appointment.AppointmentDate.DayOfWeek);

            var sb = new StringBuilder();
            sb.AppendLine($"تذكير بموعدك القادم 🔔");
            sb.AppendLine($"أستاذ/ة {clientName} العزيز/ة، نذكرك بموعد جلستك القادمة لدى *{_settings.ClinicName}*:");
            sb.AppendLine();
            sb.AppendLine($"📅 *اليوم والتاريخ:* {dayName} {dateStr}");
            sb.AppendLine($"⏰ *الوقت:* {appointment.StartTime} - {appointment.EndTime}");
            sb.AppendLine($"👨‍⚕️ *مع الأخصائي:* {doctorName}");
            sb.AppendLine();
            sb.AppendLine($"يرجى الحضور قبل الموعد بـ 10 دقائق. للاستفسار أو الاعتذار: {_settings.SenderNumber}");
            sb.AppendLine("مع أطيب تمنياتنا لك بالصحة والسلامة! 🌿");

            return sb.ToString();
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
