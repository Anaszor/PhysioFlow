using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhysioClinic.Data;
using PhysioClinic.Models;
using PhysioClinic.ViewModels;

namespace PhysioClinic.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly PhysioDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            PhysioDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var destination = (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    ? model.ReturnUrl
                    : Url.Action("Index", "Dashboard") ?? "/";

                Response.ContentType = "text/html; charset=utf-8";
                return LocalRedirect(destination);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "تم حظر الحساب مؤقتاً بسبب تكرار محاولات الدخول الخاطئة.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
            return View(model);
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "تم تسجيل الخروج بنجاح.";
            return RedirectToAction("Login", "Account");
        }

        // GET: Account/AccessDenied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ==========================================
        // Staff & Roles Management (Admin Only)
        // ==========================================

        // GET: Account/Users
        [HttpGet]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .Include(u => u.Therapist)
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();

            var list = new List<UserItemViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                list.Add(new UserItemViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? "",
                    Role = roles.FirstOrDefault() ?? "بدون دور",
                    RoleTitle = user.RoleTitle,
                    TherapistId = user.TherapistId,
                    LinkedTherapist = user.Therapist?.FullName,
                    CreatedAt = user.CreatedAt
                });
            }

            return View(list);
        }

        // GET: Account/CreateUser
        [HttpGet]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> CreateUser()
        {
            ViewBag.Therapists = await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync();
            return View(new CreateStaffViewModel());
        }

        // POST: Account/CreateUser
        [HttpPost]
        [Authorize(Roles = ClinicRoles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateStaffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Therapists = await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync();
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "هذا البريد الإلكتروني مسجل مسبقاً لمستخدم آخر.");
                ViewBag.Therapists = await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync();
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                RoleTitle = model.RoleTitle ?? ClinicRoles.GetArabicTitle(model.Role),
                TherapistId = model.Role == ClinicRoles.Doctor ? model.TherapistId : null,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                ViewBag.Therapists = await _context.Therapists.Where(t => t.IsActive).OrderBy(t => t.FullName).ToListAsync();
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }
            await _userManager.AddToRoleAsync(user, model.Role);

            TempData["SuccessMessage"] = $"تم إضافة المستخدم ({model.FullName}) وتعيين دور ({ClinicRoles.GetArabicTitle(model.Role)}) بنجاح.";
            return RedirectToAction(nameof(Users));
        }

        // GET: Account/EditUser/5
        [HttpGet]
        [Authorize(Roles = ClinicRoles.Admin)]
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var user = await _userManager.Users
                .Include(u => u.Therapist)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var currentRole = roles.FirstOrDefault() ?? ClinicRoles.Receptionist;

            var model = new EditStaffViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? "",
                Role = currentRole,
                RoleTitle = user.RoleTitle,
                TherapistId = user.TherapistId
            };

            ViewBag.Therapists = await _context.Therapists.OrderBy(t => t.FullName).ToListAsync();
            return View(model);
        }

        // POST: Account/EditUser/5
        [HttpPost]
        [Authorize(Roles = ClinicRoles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditStaffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Therapists = await _context.Therapists.OrderBy(t => t.FullName).ToListAsync();
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // Check if email changed and is taken
            if (user.Email != model.Email)
            {
                var existing = await _userManager.FindByEmailAsync(model.Email);
                if (existing != null && existing.Id != model.Id)
                {
                    ModelState.AddModelError("Email", "هذا البريد الإلكتروني مسجل مسبقاً لمستخدم آخر.");
                    ViewBag.Therapists = await _context.Therapists.OrderBy(t => t.FullName).ToListAsync();
                    return View(model);
                }
                user.Email = model.Email;
                user.UserName = model.Email;
            }

            user.FullName = model.FullName;
            user.RoleTitle = model.RoleTitle ?? ClinicRoles.GetArabicTitle(model.Role);
            user.TherapistId = model.Role == ClinicRoles.Doctor ? model.TherapistId : null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                ViewBag.Therapists = await _context.Therapists.OrderBy(t => t.FullName).ToListAsync();
                return View(model);
            }

            // Update role if changed
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(model.Role));
                }
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            // Password reset if provided
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!passResult.Succeeded)
                {
                    foreach (var err in passResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, err.Description);
                    }
                    ViewBag.Therapists = await _context.Therapists.OrderBy(t => t.FullName).ToListAsync();
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = $"تم تحديث بيانات المستخدم ({user.FullName}) بنجاح.";
            return RedirectToAction(nameof(Users));
        }

        // POST: Account/DeleteUser
        [HttpPost]
        [Authorize(Roles = ClinicRoles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && currentUser.Id == id)
            {
                TempData["ErrorMessage"] = "لا يمكنك حذف حسابك الحالي أثناء تسجيل الدخول منه.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
                TempData["SuccessMessage"] = "تم حذف حساب المستخدم بنجاح.";
            }

            return RedirectToAction(nameof(Users));
        }
    }
}
