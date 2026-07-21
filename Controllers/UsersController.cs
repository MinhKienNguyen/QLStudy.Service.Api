using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLStudy.API.Data;
using QLStudy.API.Models;

namespace QLStudy.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : BaseApiController
    {
        private readonly PasswordHasher<User> _passwordHasher;

        public UsersController(QLStudyDbContext context) : base(context)
        {
            _passwordHasher = new PasswordHasher<User>();
        }

        public class UserDto
        {
            public int Id { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string? Password { get; set; }
            public string Role { get; set; } = "Teacher";
            public string Status { get; set; } = "Active";
            public List<int> SubjectIds { get; set; } = new List<int>();
        }

        // GET: api/users
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            var users = await _context.Users
                .Include(u => u.UserSubjects)
                .ThenInclude(us => us.Subject)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    u.Role,
                    u.Status,
                    SubjectIds = u.UserSubjects.Select(us => us.SubjectId).ToList(),
                    Subjects = u.UserSubjects.Select(us => us.Subject!.Name).ToList()
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            var user = await _context.Users
                .Include(u => u.UserSubjects)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.Role,
                user.Status,
                SubjectIds = user.UserSubjects.Select(us => us.SubjectId).ToList()
            });
        }

        // POST: api/users
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] UserDto dto)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { message = "Email này đã được sử dụng." });
            }

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role,
                Status = dto.Status
            };

            // Hash password
            string rawPassword = string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password;
            user.PasswordHash = _passwordHasher.HashPassword(user, rawPassword);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Update subjects
            if (dto.SubjectIds != null && dto.SubjectIds.Any())
            {
                foreach (var subId in dto.SubjectIds)
                {
                    _context.UserSubjects.Add(new UserSubject
                    {
                        UserId = user.Id,
                        SubjectId = subId
                    });
                }
                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { id = user.Id });
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserDto dto)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            if (id != dto.Id) return BadRequest();

            var user = await _context.Users
                .Include(u => u.UserSubjects)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound();

            // Check if email already exists on another user
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
            {
                return BadRequest(new { message = "Email này đã được sử dụng bởi người dùng khác." });
            }

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.PhoneNumber = dto.PhoneNumber;
            user.Role = dto.Role;
            user.Status = dto.Status;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            }

            // Update user details
            _context.Entry(user).State = EntityState.Modified;

            // Clear existing subjects and re-add
            _context.UserSubjects.RemoveRange(user.UserSubjects);

            if (dto.SubjectIds != null && dto.SubjectIds.Any())
            {
                foreach (var subId in dto.SubjectIds)
                {
                    _context.UserSubjects.Add(new UserSubject
                    {
                        UserId = user.Id,
                        SubjectId = subId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            // Prevent deleting self
            if (currentUser.Id == id)
            {
                return BadRequest(new { message = "Bạn không thể tự xóa tài khoản của chính mình." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/users/permissions
        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissions()
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            var permissions = await _context.ScreenPermissions
                .OrderBy(p => p.Role)
                .ThenBy(p => p.ScreenKey)
                .ToListAsync();

            return Ok(permissions);
        }

        // PUT: api/users/permissions
        [HttpPut("permissions")]
        public async Task<IActionResult> UpdatePermissions([FromBody] List<ScreenPermission> permissions)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null) return Unauthorized();
            if (currentUser.Role != "Manager") return Forbid();

            foreach (var perm in permissions)
            {
                var existing = await _context.ScreenPermissions
                    .FirstOrDefaultAsync(p => p.Role == perm.Role && p.ScreenKey == perm.ScreenKey);

                if (existing != null)
                {
                    existing.IsAllowed = perm.IsAllowed;
                    _context.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    _context.ScreenPermissions.Add(perm);
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
