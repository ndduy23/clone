using BookDb.Models;
using System.Security.Claims;

namespace BookDb.Services.Implementations
{
    public interface IJwtService
    {
        string GenerateToken(User user, IList<string> roles);
        Task<string> GenerateRefreshTokenAsync(User user, string jwtId);
        string GenerateRefreshToken();
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        Task<bool> ValidateRefreshTokenAsync(string refreshToken, string jwtId);
        Task RevokeRefreshTokenAsync(string refreshToken, string reason);
        Task RevokeAllUserTokensAsync(string userId, string reason);
        Task MarkRefreshTokenAsUsedAsync(string refreshToken);
        Task<int> GetActiveTokenCountAsync(string userId);
        Task CleanupExpiredTokensAsync();
    }
}