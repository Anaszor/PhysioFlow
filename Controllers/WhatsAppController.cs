using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PhysioClinic.Models;
using PhysioClinic.Services.WhatsApp;

namespace PhysioClinic.Controllers
{
    [Authorize]
    public class WhatsAppController : Controller
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly WhatsAppSettings _settings;
        private readonly ILogger<WhatsAppController> _logger;

        public WhatsAppController(
            IWhatsAppService whatsAppService,
            IOptions<WhatsAppSettings> options,
            ILogger<WhatsAppController> logger)
        {
            _whatsAppService = whatsAppService;
            _settings = options.Value;
            _logger = logger;
        }

        // GET: /WhatsApp
        public async Task<IActionResult> Index()
        {
            var status = await _whatsAppService.GetSessionStatusAsync();
            string? qrCode = null;

            if (status.Status == "qr_ready")
            {
                qrCode = await _whatsAppService.GetQrCodeAsync();
            }

            ViewBag.Settings = _settings;
            ViewBag.Status = status;
            ViewBag.QrCode = qrCode;
            ViewBag.PairingCode = TempData["PairingCode"] as string;

            return View();
        }

        // POST: /WhatsApp/Start
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> Start()
        {
            var success = await _whatsAppService.StartSessionAsync();
            if (success)
            {
                TempData["SuccessMessage"] = "تم بدء تشغيل جلسة الواتساب بنجاح. يمكنك الآن ربط رقمك.";
            }
            else
            {
                TempData["ErrorMessage"] = "فشل بدء جلسة الواتساب. يرجى التأكد من تشغيل البوابة على المنفذ 2785.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /WhatsApp/RequestPairing
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{ClinicRoles.Admin},{ClinicRoles.Receptionist}")]
        public async Task<IActionResult> RequestPairing(string? phoneNumber)
        {
            var phone = string.IsNullOrWhiteSpace(phoneNumber) ? _settings.SenderNumber : phoneNumber;
            var result = await _whatsAppService.RequestPairingCodeAsync(phone);

            if (result.Success && !string.IsNullOrEmpty(result.PairingCode))
            {
                TempData["PairingCode"] = result.PairingCode;
                TempData["SuccessMessage"] = $"تم توليد كود الربط بنجاح لرقمك ({phone})! الكود هو: {result.PairingCode}";
            }
            else
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "تعذر توليد كود الربط. تأكد من تشغيل الجلسة وبوابة الواتساب.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /WhatsApp/SendTest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTest(string phone, string message)
        {
            if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "يرجى إدخال رقم الهاتف ونص الرسالة.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _whatsAppService.SendTextMessageAsync(phone, message);
            if (result.Success)
            {
                TempData["SuccessMessage"] = $"تم إرسال رسالة الواتساب بنجاح إلى ({phone}).";
            }
            else
            {
                TempData["ErrorMessage"] = $"فشل إرسال الرسالة: {result.ErrorMessage}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /WhatsApp/LiveStatus (AJAX polling)
        [HttpGet]
        public async Task<IActionResult> LiveStatus()
        {
            var status = await _whatsAppService.GetSessionStatusAsync();
            string? qr = null;
            if (status.Status == "qr_ready")
            {
                qr = await _whatsAppService.GetQrCodeAsync();
            }

            return Json(new
            {
                isServerReachable = status.IsServerReachable,
                status = status.Status,
                isReady = status.IsReady,
                phone = status.Phone,
                pushName = status.PushName,
                errorMessage = status.ErrorMessage,
                qrCode = qr
            });
        }
    }
}
