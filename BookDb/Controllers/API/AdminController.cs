using BookDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookDb.Controllers.Api
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Policy = Policies.CanManageUsers)]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // GET: api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u =>
                        u.Email.Contains(search) ||
                        (u.FullName != null && u.FullName.Contains(search)));
                }

                var totalUsers = await query.CountAsync();
                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var userDtos = new List<object>();
                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userDtos.Add(new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.IsActive,
                        CreatedAt = user.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        LastLoginAt = user.LastLoginAt?.ToString("dd/MM/yyyy HH:mm"),
                        Roles = roles
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = userDtos,
                    total = totalUsers,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // GET: api/admin/users/{id}
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.IsActive,
                        CreatedAt = user.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        LastLoginAt = user.LastLoginAt?.ToString("dd/MM/yyyy HH:mm"),
                        Roles = roles
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/admin/users/{id}/toggle-active
        [HttpPost("users/{id}/toggle-active")]
        [Authorize(Policy = Policies.RequireAdminRole)]
        public async Task<IActionResult> ToggleUserActive(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Prevent disabling admin account
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(Roles.Admin) && !user.IsActive)
                {
                    return BadRequest(new { success = false, message = "Không thể khóa tài khoản Admin" });
                }

                user.IsActive = !user.IsActive;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Ok(new
                    {
                        success = true,
                        message = user.IsActive ? "Đã kích hoạt tài khoản" : "Đã khóa tài khoản",
                        isActive = user.IsActive
                    });
                }

                return BadRequest(new { success = false, message = "Không thể cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user active status {UserId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/admin/users/{id}/assign-role
        [HttpPost("users/{id}/assign-role")]
        public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Check if role exists
                if (!await _roleManager.RoleExistsAsync(model.RoleName))
                {
                    return BadRequest(new { success = false, message = "Role không tồn tại" });
                }

                // Check if user already has the role
                if (await _userManager.IsInRoleAsync(user, model.RoleName))
                {
                    return BadRequest(new { success = false, message = "Người dùng đã có role này" });
                }

                var result = await _userManager.AddToRoleAsync(user, model.RoleName);
                if (result.Succeeded)
                {
                    return Ok(new
                    {
                        success = true,
                        message = $"Đã gán role {model.RoleName} cho người dùng"
                    });
                }

                return BadRequest(new { success = false, message = "Không thể gán role" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning role to user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // DELETE: api/admin/users/{id}/remove-role
        [HttpDelete("users/{id}/remove-role")]
        public async Task<IActionResult> RemoveRole(string id, [FromQuery] string roleName)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Prevent removing Admin role from last admin
                if (roleName == Roles.Admin)
                {
                    var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
                    if (admins.Count <= 1)
                    {
                        return BadRequest(new { success = false, message = "Không thể xóa role Admin khỏi admin cuối cùng" });
                    }
                }

                var result = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (result.Succeeded)
                {
                    return Ok(new
                    {
                        success = true,
                        message = $"Đã xóa role {roleName} khỏi người dùng"
                    });
                }

                return BadRequest(new { success = false, message = "Không thể xóa role" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role from user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // GET: api/admin/roles
        [HttpGet("roles")]
        public IActionResult GetRoles()
        {
            try
            {
                var roleDescriptions = Roles.GetRoleDescriptions();
                var roles = roleDescriptions.Select(r => new
                {
                    Key = r.Key,
                    r.Value.Name,
                    r.Value.Description,
                    r.Value.Permissions
                });

                return Ok(new
                {
                    success = true,
                    data = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // DELETE: api/admin/users/{id}
        [HttpDelete("users/{id}")]
        [Authorize(Policy = Policies.RequireAdminRole)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Prevent deleting admin account
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(Roles.Admin))
                {
                    var admins = await _userManager.GetUsersInRoleAsync(Roles.Admin);
                    if (admins.Count <= 1)
                    {
                        return BadRequest(new { success = false, message = "Không thể xóa admin cuối cùng" });
                    }
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    return Ok(new { success = true, message = "Đã xóa người dùng" });
                }

                return BadRequest(new { success = false, message = "Không thể xóa người dùng" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }
    }

    public class AssignRoleDto
    {
        public string RoleName { get; set; } = string.Empty;
    }
}