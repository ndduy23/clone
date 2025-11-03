using BookDb.Models;
using BookDb.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace BookDb.Services.Implementations
{
    public class JwtService : IJwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<JwtService> _logger;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JwtService(
            IOptions<JwtSettings> jwtSettings, 
            ILogger<JwtService> logger,
            IRefreshTokenRepository refreshTokenRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
            _refreshTokenRepository = refreshTokenRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public string GenerateToken(User user, IList<string> roles)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                    new Claim(ClaimTypes.Name, user.FullName ?? user.Email ?? string.Empty),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                    new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
                };

                // Add roles to claims
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: _jwtSettings.Issuer,
                    audience: _jwtSettings.Audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
                    signingCredentials: credentials
                );

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user {UserId}", user.Id);
                throw;
            }
        }

        public async Task<string> GenerateRefreshTokenAsync(User user, string jwtId)
        {
            try
            {
                // Generate random token
                var randomNumber = new byte[64];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomNumber);
                var token = Convert.ToBase64String(randomNumber);

                // Get device info and IP
                var httpContext = _httpContextAccessor.HttpContext;
                var deviceInfo = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";
                var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                // Create refresh token entity
                var refreshToken = new RefreshToken
                {
                    UserId = user.Id,
                    Token = token,
                    JwtId = jwtId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                    DeviceInfo = deviceInfo.Length > 500 ? deviceInfo.Substring(0, 500) : deviceInfo,
                    IpAddress = ipAddress.Length > 50 ? ipAddress.Substring(0, 50) : ipAddress
                };

                // Save to database
                await _refreshTokenRepository.AddAsync(refreshToken);

                _logger.LogInformation("Generated refresh token for user {UserId}, JwtId: {JwtId}", user.Id, jwtId);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token for user {UserId}", user.Id);
                throw;
            }
        }

        public string GenerateRefreshToken()
        {
            // Legacy method - keep for backward compatibility
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidAudience = _jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
                    ValidateLifetime = false // Don't validate expiration for refresh
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating expired token");
                return null;
            }
        }

        public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, string jwtId)
        {
            try
            {
                var storedToken = await _refreshTokenRepository.GetByTokenAsync(refreshToken);

                if (storedToken == null)
                {
                    _logger.LogWarning("Refresh token not found in database");
                    return false;
                }

                if (storedToken.JwtId != jwtId)
                {
                    _logger.LogWarning("JWT ID mismatch. Expected: {Expected}, Got: {Got}", 
                        storedToken.JwtId, jwtId);
                    return false;
                }

                if (!storedToken.IsActive)
                {
                    _logger.LogWarning("Refresh token is not active. IsRevoked: {IsRevoked}, IsUsed: {IsUsed}, Expired: {Expired}",
                        storedToken.IsRevoked, storedToken.IsUsed, storedToken.ExpiresAt < DateTime.UtcNow);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating refresh token");
                return false;
            }
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken, string reason)
        {
            try
            {
                await _refreshTokenRepository.RevokeTokenAsync(refreshToken, reason);
                _logger.LogInformation("Revoked refresh token. Reason: {Reason}", reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                throw;
            }
        }

        public async Task RevokeAllUserTokensAsync(string userId, string reason)
        {
            try
            {
                await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, reason);
                _logger.LogInformation("Revoked all tokens for user {UserId}. Reason: {Reason}", userId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all user tokens for {UserId}", userId);
                throw;
            }
        }

        public async Task MarkRefreshTokenAsUsedAsync(string refreshToken)
        {
            try
            {
                var storedToken = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
                if (storedToken != null)
                {
                    storedToken.IsUsed = true;
                    storedToken.UsedAt = DateTime.UtcNow;
                    await _refreshTokenRepository.UpdateAsync(storedToken);
                    _logger.LogInformation("Marked refresh token as used");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking refresh token as used");
                throw;
            }
        }

        public async Task<int> GetActiveTokenCountAsync(string userId)
        {
            return await _refreshTokenRepository.CountActiveTokensByUserAsync(userId);
        }

        public async Task CleanupExpiredTokensAsync()
        {
            try
            {
                await _refreshTokenRepository.DeleteExpiredTokensAsync();
                _logger.LogInformation("Cleaned up expired refresh tokens");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
            }
        }
    }
}