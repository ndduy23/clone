using BookDb.Models;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BookDb.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IJwtService jwtService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto model)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Email đã được sử dụng"
                    };
                }

                // Create new user
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = $"Đăng ký thất bại: {errors}"
                    };
                }

                // Assign default role
                await _userManager.AddToRoleAsync(user, "User");

                _logger.LogInformation("User {Email} registered successfully", model.Email);

                // Generate tokens
                var roles = await _userManager.GetRolesAsync(user);
                var accessToken = _jwtService.GenerateToken(user, roles);

                // Extract JWT ID from access token
                var jwtId = GetJwtId(accessToken);

                // Generate and save refresh token to database
                var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, jwtId);

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Đăng ký thành công",
                    Token = accessToken,
                    RefreshToken = refreshToken,
                    Expiration = DateTime.UtcNow.AddHours(1),
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email!,
                        FullName = user.FullName
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", model.Email);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Có lỗi xảy ra trong quá trình đăng ký"
                };
            }
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto model)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng"
                    };
                }

                if (!user.IsActive)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Tài khoản đã bị khóa"
                    };
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
                if (!result.Succeeded)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Email hoặc mật khẩu không đúng"
                    };
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                // Generate tokens
                var roles = await _userManager.GetRolesAsync(user);
                var accessToken = _jwtService.GenerateToken(user, roles);

                // Extract JWT ID from access token
                var jwtId = GetJwtId(accessToken);

                // Generate and save refresh token to database
                var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, jwtId);

                _logger.LogInformation("User {Email} logged in successfully", model.Email);

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Đăng nhập thành công",
                    Token = accessToken,
                    RefreshToken = refreshToken,
                    Expiration = DateTime.UtcNow.AddHours(1),
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email!,
                        FullName = user.FullName
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", model.Email);
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Có lỗi xảy ra trong quá trình đăng nhập"
                };
            }
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto model)
        {
            try
            {
                var principal = _jwtService.GetPrincipalFromExpiredToken(model.Token);
                if (principal == null)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Token không hợp lệ"
                    };
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(jwtId))
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Token không hợp lệ"
                    };
                }

                // Validate refresh token against database
                var isValid = await _jwtService.ValidateRefreshTokenAsync(model.RefreshToken, jwtId);
                if (!isValid)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Refresh token không hợp lệ hoặc đã hết hạn"
                    };
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return new AuthResponseDto
                    {
                        Success = false,
                        Message = "Người dùng không tồn tại hoặc đã bị khóa"
                    };
                }

                // Mark old refresh token as used
                await _jwtService.MarkRefreshTokenAsUsedAsync(model.RefreshToken);

                // Generate new tokens
                var roles = await _userManager.GetRolesAsync(user);
                var newAccessToken = _jwtService.GenerateToken(user, roles);

                // Extract new JWT ID
                var newJwtId = GetJwtId(newAccessToken);

                // Generate new refresh token and save to database
                var newRefreshToken = await _jwtService.GenerateRefreshTokenAsync(user, newJwtId);

                _logger.LogInformation("Tokens refreshed successfully for user {UserId}", userId);

                return new AuthResponseDto
                {
                    Success = true,
                    Token = newAccessToken,
                    RefreshToken = newRefreshToken,
                    Expiration = DateTime.UtcNow.AddHours(1),
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email!,
                        FullName = user.FullName
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Có lỗi xảy ra khi làm mới token"
                };
            }
        }

        public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordDto model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return false;
                }

                var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!result.Succeeded)
                {
                    _logger.LogWarning("Password change failed for user {UserId}", userId);
                    return false;
                }

                // Revoke all refresh tokens after password change for security
                await _jwtService.RevokeAllUserTokensAsync(userId, "Password changed");

                _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                return false;
            }
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task LogoutAsync(string userId, string refreshToken)
        {
            try
            {
                // Revoke the specific refresh token
                await _jwtService.RevokeRefreshTokenAsync(refreshToken, "User logged out");

                _logger.LogInformation("User {UserId} logged out", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout for user {UserId}", userId);
            }
        }

        public async Task LogoutAllDevicesAsync(string userId)
        {
            try
            {
                // Revoke all refresh tokens
                await _jwtService.RevokeAllUserTokensAsync(userId, "Logout from all devices");

                _logger.LogInformation("User {UserId} logged out from all devices", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out all devices for user {UserId}", userId);
            }
        }

        private string GetJwtId(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.Id;
        }
    }
}