using BookDb.Models;

namespace BookDb.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken?> GetByJwtIdAsync(string jwtId);
        Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(string userId);
        Task<List<RefreshToken>> GetAllTokensByUserIdAsync(string userId);
        Task AddAsync(RefreshToken refreshToken);
        Task UpdateAsync(RefreshToken refreshToken);
        Task RevokeAllUserTokensAsync(string userId, string reason);
        Task RevokeTokenAsync(string token, string reason);
        Task DeleteExpiredTokensAsync();
        Task<int> CountActiveTokensByUserAsync(string userId);
    }
}