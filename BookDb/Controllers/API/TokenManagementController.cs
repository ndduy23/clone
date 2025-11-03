using BookDb.Repositories.Interfaces;
using BookDb.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookDb.Controllers.Api
{
    [Route("api/token")]
    [ApiController]
    [Authorize]
    public class TokenManagementController : ControllerBase
    {
        private readonly IJwtService _jwtService;
        private readonly IRefreshTokenRepository _tokenRepository;
        private readonly IAuthService _authService;
        private readonly ILogger<TokenManagementController> _logger;

        public TokenManagementController(
            IJwtService jwtService,
            IRefreshTokenRepository tokenRepository,
            IAuthService authService,
            ILogger<TokenManagementController> logger)
        {
            _jwtService = jwtService;
            _tokenRepository = tokenRepository;
            _authService = authService;
            _logger = logger;
        }

        // GET: api/token/my-devices
        [HttpGet("my-devices")]
        public async Task<IActionResult> GetMyDevices()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được người dùng" });
                }

                var tokens = await _tokenRepository.GetActiveTokensByUserIdAsync(userId);

                var devices = tokens.Select(t => new
                {
                    t.Id,
                    DeviceInfo = t.DeviceInfo ?? "Unknown Device",
                    IpAddress = t.IpAddress ?? "Unknown",
                    CreatedAt = t.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    ExpiresAt = t.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
                    IsCurrent = IsCurrentToken(t.Token)
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = devices,
                    count = devices.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user devices");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/token/logout-device/{tokenId}
        [HttpPost("logout-device/{tokenId}")]
        public async Task<IActionResult> LogoutDevice(int tokenId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được người dùng" });
                }

                var tokens = await _tokenRepository.GetAllTokensByUserIdAsync(userId);
                var token = tokens.FirstOrDefault(t => t.Id == tokenId);

                if (token == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thiết bị" });
                }

                if (token.UserId != userId)
                {
                    return Forbid();
                }

                await _jwtService.RevokeRefreshTokenAsync(token.Token, "Logged out by user");

                return Ok(new
                {
                    success = true,
                    message = "Đã đăng xuất khỏi thiết bị"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out device {TokenId}", tokenId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // POST: api/token/logout-all-devices
        [HttpPost("logout-all-devices")]
        public async Task<IActionResult> LogoutAllDevices()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được người dùng" });
                }

                await _authService.LogoutAllDevicesAsync(userId);

                return Ok(new
                {
                    success = true,
                    message = "Đã đăng xuất khỏi tất cả thiết bị"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging out all devices");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        // GET: api/token/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetTokenStats()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được người dùng" });
                }

                var activeCount = await _jwtService.GetActiveTokenCountAsync(userId);
                var allTokens = await _tokenRepository.GetAllTokensByUserIdAsync(userId);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        ActiveSessions = activeCount,
                        TotalTokensCreated = allTokens.Count,
                        RevokedTokens = allTokens.Count(t => t.IsRevoked),
                        UsedTokens = allTokens.Count(t => t.IsUsed),
                        ExpiredTokens = allTokens.Count(t => t.ExpiresAt < DateTime.UtcNow)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token stats");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra" });
            }
        }

        private bool IsCurrentToken(string token)
        {
            // Get refresh token from cookie or header
            var currentToken = Request.Cookies["refreshToken"];
            return currentToken == token;
        }
    }
}