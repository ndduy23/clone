namespace BookDb.Middleware
{
    /// <summary>
    /// Middleware to extract JWT token from cookie or Authorization header
    /// and add it to the Authorization header for authentication
    /// </summary>
    public class JwtCookieMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtCookieMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if there's already an Authorization header
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                // Try to get token from cookie
                var token = context.Request.Cookies["token"];

                if (!string.IsNullOrEmpty(token))
                {
                    // Add token to Authorization header
                    context.Request.Headers.Add("Authorization", $"Bearer {token}");
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension method to register the middleware
    /// </summary>
    public static class JwtCookieMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtCookie(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtCookieMiddleware>();
        }
    }
}