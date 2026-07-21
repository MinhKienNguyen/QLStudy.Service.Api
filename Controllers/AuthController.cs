using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseApiController
    {
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(QLStudyDbContext context) : base(context)
        {
            _passwordHasher = new PasswordHasher<User>();
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        public class ChangePasswordRequest
        {
            public string OldPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || user.Status != "Active")
            {
                return BadRequest(new { message = "Email không tồn tại hoặc tài khoản đã bị khóa." });
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { message = "Mật khẩu không chính xác." });
            }

            // Generate JWT
            var config = (IConfiguration?)HttpContext.RequestServices.GetService(typeof(IConfiguration));
            var secretKey = config?["Jwt:SecretKey"];
            var token = JwtHelper.GenerateToken(user, secretKey);

            // Set HttpOnly cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/"
            };
            Response.Cookies.Append("qlstudy_session", token, cookieOptions);

            // Fetch user permissions
            var permissions = await _context.ScreenPermissions
                .Where(p => p.Role == user.Role)
                .ToDictionaryAsync(p => p.ScreenKey, p => p.IsAllowed);

            return Ok(new
            {
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.Role,
                    Token = token // fallback for compatibility
                },
                permissions
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            Response.Cookies.Delete("qlstudy_session", new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
            return Ok(new { message = "Đăng xuất thành công." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return BadRequest(new { message = "Email không tồn tại trong hệ thống." });
            }

            // Reset password to default '123456'
            string defaultPassword = "123456";
            user.PasswordHash = _passwordHasher.HashPassword(user, defaultPassword);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Mật khẩu đã được khôi phục thành công về mặc định.",
                defaultPassword = defaultPassword
            });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.OldPassword);
            if (result == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { message = "Mật khẩu cũ không chính xác." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công." });
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return Unauthorized();

            // Fetch user permissions
            var permissions = await _context.ScreenPermissions
                .Where(p => p.Role == user.Role)
                .ToDictionaryAsync(p => p.ScreenKey, p => p.IsAllowed);

            return Ok(new
            {
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.Role,
                    user.Token
                },
                permissions
            });
        }
    }
}
