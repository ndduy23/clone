using BookDb.Models;
using BookDb.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookDb.Controllers.Api
{
    [Route("api/auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<AuthApiController> _logger;

        public AuthApiController(
            IAuthService authService,
            UserManager<User> userManager,
            ILogger<AuthApiController> logger)
        {
            _authService = authService;
            _userManager = userManager;
            _logger = logger;
        }

        // POST: api/auth/register
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            try
            {
                _logger.LogInformation("=== REGISTER ATTEMPT ===");
                _logger.LogInformation("Email: {Email}, FullName: {FullName}", model.Email, model.FullName ?? "null");

                // Validate model
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    _logger.LogWarning("ModelState invalid: {Errors}", string.Join(", ", errors));

                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors)
                    });
                }

                // Check if email is valid
                if (string.IsNullOrWhiteSpace(model.Email))
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Email không được để trống"
                    });
                }

                // Check if passwords match
                if (model.Password != model.ConfirmPassword)
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Mật khẩu xác nhận không khớp"
                    });
                }

                // Call register service
                var result = await _authService.RegisterAsync(model);

                _logger.LogInformation("Register service returned: Success={Success}, Message={Message}",
                    result.Success, result.Message ?? "null");

                // Verify user was created
                if (result.Success)
                {
                    var userInDb = await _userManager.FindByEmailAsync(model.Email);
                    if (userInDb != null)
                    {
                        _logger.LogInformation("✓ User created successfully in DB. ID: {UserId}, Email: {Email}",
                            userInDb.Id, userInDb.Email);
                    }
                    else
                    {
                        _logger.LogError("✗ User NOT found in DB after registration!");
                    }
                }

                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during registration for email: {Email}", model.Email);
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = $"Lỗi hệ thống: {ex.Message}"
                });
            }
        }

        // POST: api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            try
            {
                _logger.LogInformation("=== LOGIN ATTEMPT ===");
                _logger.LogInformation("Email: {Email}, RememberMe: {RememberMe}", model.Email, model.RememberMe);

                // Validate model
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ: " + string.Join(", ", errors)
                    });
                }

                // Check if user exists
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found for email {Email}", model.Email);
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng"
                    });
                }

                _logger.LogInformation("User found: ID={UserId}, IsActive={IsActive}", user.Id, user.IsActive);

                // Call login service
                var result = await _authService.LoginAsync(model);

                _logger.LogInformation("Login service returned: Success={Success}", result.Success);

                if (!result.Success)
                {
                    return Unauthorized(result);
                }

                // Set cookies for server-side access
                if (result.Token != null)
                {
                    Response.Cookies.Append("token", result.Token, new CookieOptions
                    {
                        HttpOnly = false, // Allow JavaScript access
                        Secure = false, // Set to true in production with HTTPS
                        SameSite = SameSiteMode.Lax,
                        Expires = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(1)
                    });
                }

                if (result.RefreshToken != null)
                {
                    Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during login for email: {Email}", model.Email);
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = "Lỗi hệ thống"
                });
            }
        }

        // POST: api/auth/logout
        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            try
            {
                _logger.LogInformation("User logged out");

                // Clear cookies
                Response.Cookies.Delete("token");
                Response.Cookies.Delete("refreshToken");

                return Ok(new { success = true, message = "Đã đăng xuất" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
            }
        }

        // GET: api/auth/me
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "Unauthorized" });
                }

                var user = await _authService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.IsActive,
                        Roles = roles
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
            }
        }

        // POST: api/auth/refresh-token
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto model)
        {
            try
            {
                _logger.LogInformation("Refresh token attempt");

                var result = await _authService.RefreshTokenAsync(model);

                if (!result.Success)
                {
                    return Unauthorized(result);
                }

                // Update cookies
                if (result.Token != null)
                {
                    Response.Cookies.Append("token", result.Token, new CookieOptions
                    {
                        HttpOnly = false,
                        Secure = false,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddHours(1)
                    });
                }

                if (result.RefreshToken != null)
                {
                    Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = "Lỗi hệ thống"
                });
            }
        }

        // POST: api/auth/change-password
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "Unauthorized" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                var success = await _authService.ChangePasswordAsync(userId, model);

                if (!success)
                {
                    return BadRequest(new { success = false, message = "Mật khẩu hiện tại không đúng" });
                }

                return Ok(new { success = true, message = "Đổi mật khẩu thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
            }
        }

        // DEBUG ENDPOINT - Remove in production!
        [HttpGet("debug/users")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = _userManager.Users.ToList();
                var userList = new List<object>();

                foreach (var user in users)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    userList.Add(new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.IsActive,
                        CreatedAt = user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        Roles = roles
                    });
                }

                return Ok(new
                {
                    success = true,
                    count = users.Count,
                    users = userList
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DEBUG ENDPOINT - Test connection
        [HttpGet("debug/test")]
        [AllowAnonymous]
        public IActionResult TestConnection()
        {
            return Ok(new
            {
                success = true,
                message = "API is working",
                timestamp = DateTime.UtcNow
            });
        }
    }
}