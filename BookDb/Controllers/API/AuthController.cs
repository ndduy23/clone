using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookDb.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        // GET: /auth/login
        [HttpGet("login")]
        [AllowAnonymous]
        public IActionResult Login()
        {
            _logger.LogInformation("Login page accessed");

            // If already logged in, redirect to documents
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Documents");
            }

            return View();
        }

        // GET: /auth/register
        [HttpGet("register")]
        [AllowAnonymous]
        public IActionResult Register()
        {
            _logger.LogInformation("Register page accessed");

            // If already logged in, redirect to documents
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Documents");
            }

            return View();
        }

        // GET: /auth/logout
        [HttpGet("logout")]
        public IActionResult Logout()
        {
            _logger.LogInformation("User logged out");

            // Clear cookies
            Response.Cookies.Delete("token");
            Response.Cookies.Delete("refreshToken");

            return RedirectToAction("Login");
        }

        // GET: /auth/profile
        [HttpGet("profile")]
        [Authorize]
        public IActionResult Profile()
        {
            return View();
        }
    }
}