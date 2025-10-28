using BookDb.Models;
using System.Security.Claims;

namespace BookDb.Services.Implementations
{
    public interface IJwtService
    {
        string GenerateToken(User user, IList<string> roles);
        string GenerateRefreshToken();
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    }
}