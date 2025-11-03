using BookDb.Models;

namespace BookDb.Services.Implementations
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterAsync(RegisterDto model);
        Task<AuthResponseDto> LoginAsync(LoginDto model);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto model);
        Task<bool> ChangePasswordAsync(string userId, ChangePasswordDto model);
        Task<User?> GetUserByIdAsync(string userId);
        Task LogoutAsync(string userId, string refreshToken);
        Task LogoutAllDevicesAsync(string userId);
    }
}