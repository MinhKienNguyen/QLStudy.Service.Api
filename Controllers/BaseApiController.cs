using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    public class BaseApiController : ControllerBase
    {
        protected readonly QLStudyDbContext _context;

        public BaseApiController(QLStudyDbContext context)
        {
            _context = context;
        }

        protected async Task<User?> GetCurrentUserAsync()
        {
            // 1. Try to read token from HttpOnly cookie
            string? token = Request.Cookies["qlstudy_session"];

            // 2. Fall back to Authorization Header
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(token)) return null;

            // 3. Validate JWT
            var config = (IConfiguration?)HttpContext.RequestServices.GetService(typeof(IConfiguration));
            var secretKey = config?["Jwt:SecretKey"];
            var principal = JwtHelper.ValidateToken(token, secretKey);
            if (principal == null) return null;

            // 4. Extract User ID claim
            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? principal.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return null;
            }

            // 5. Query user from database
            return await _context.Users
                .Include(u => u.UserSubjects)
                .FirstOrDefaultAsync(u => u.Id == userId && u.Status == "Active");
        }

        protected async Task<List<int>> GetTeacherSubjectIdsAsync(int userId)
        {
            return await _context.UserSubjects
                .Where(us => us.UserId == userId)
                .Select(us => us.SubjectId)
                .ToListAsync();
        }

        protected async Task<bool> HasScreenPermissionAsync(string screenKey)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return false;

            if (user.Role == "Manager") return true;

            var permission = await _context.ScreenPermissions
                .FirstOrDefaultAsync(p => p.Role == user.Role && p.ScreenKey == screenKey);

            return permission?.IsAllowed ?? false;
        }
    }
}
