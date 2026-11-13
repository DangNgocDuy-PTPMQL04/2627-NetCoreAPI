
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemoMVC.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
            [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
            [Display(Name = "Địa chỉ email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [StringLength(100, MinimumLength = 8,
                ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
            [DataType(DataType.Password)]
            [Compare("Password",
                ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            [Display(Name = "Xác nhận mật khẩu")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var existingUser =
                await _userManager.FindByEmailAsync(Input.Email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    "Input.Email",
                    "Email này đã được sử dụng.");

                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FullName = Input.FullName.Trim(),
                EmailConfirmed = false
            };

            var createResult = await _userManager.CreateAsync(
                user,
                Input.Password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return Page();
            }

            // Người đăng ký chỉ được cấp quyền User.
            var roleResult = await _userManager.AddToRoleAsync(
                user,
                "User");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                _logger.LogError(
                    "Không thể gán vai trò User cho tài khoản {Email}",
                    Input.Email);

                ModelState.AddModelError(
                    string.Empty,
                    "Không thể thiết lập quyền tài khoản. Vui lòng thử lại.");

                return Page();
            }

            // Tạo mã xác nhận email.
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(
                user);

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    userId = user.Id,
                    code = code
                },
                protocol: Request.Scheme);

            if (string.IsNullOrEmpty(callbackUrl))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Không thể tạo liên kết xác nhận email.");

                return Page();
            }

            var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl);

            var emailBody = $"""
                <div style="font-family:Arial,sans-serif;line-height:1.6">
                    <h2>Chào mừng bạn đến với DemoMVC!</h2>
                    <p>Xin chào {HtmlEncoder.Default.Encode(user.FullName ?? "")},</p>
                    <p>Cảm ơn bạn đã đăng ký tài khoản.</p>
                    <p>Vui lòng nhấn nút bên dưới để xác nhận địa chỉ email:</p>
                    <p>
                        <a href="{encodedUrl}"
                           style="background:#0d6efd;color:white;
                                  padding:12px 20px;text-decoration:none;
                                  border-radius:5px">
                            Xác nhận email
                        </a>
                    </p>
                    <p>Nếu bạn không thực hiện đăng ký này, hãy bỏ qua email.</p>
                    <p>Trân trọng,<br/>DemoMVC</p>
                </div>
                """;

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Xác nhận tài khoản DemoMVC",
                    emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Không gửi được email xác nhận tới {Email}",
                    user.Email);

                ModelState.AddModelError(
                    string.Empty,
                    "Tài khoản đã được tạo nhưng gửi email thất bại. " +
                    "Vui lòng liên hệ quản trị viên để được hỗ trợ.");

                return Page();
            }

            return RedirectToPage(
                "/RegisterConfirmation",
                new
                {
                    area = "Identity",
                    email = user.Email
                });
        }
    }
}
