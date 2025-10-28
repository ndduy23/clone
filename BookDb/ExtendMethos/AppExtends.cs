using Microsoft.AspNetCore.Builder;
using System.Net;

namespace BookDb.ExtendMethos
{
    public static class AppExtends
    {
        public static void AddStatusCodePage(this IApplicationBuilder app)
        {
            app.UseStatusCodePages(appError =>
            {
                appError.Run(async context =>
                {
                    var response = context.Response;
                    var code = response.StatusCode;

                    var content = $@"<html>
                        <head>
                            <meta charset ='UTF-8'/>
                            <title> Lỗi{code}</title>
                        </head>
                        <body>
                            <p>
                                Có lỗi xảy ra : {code} - {(HttpStatusCode)code}
                            </p>
                        </body>
                    </html>";

                    await response.WriteAsync(content);
                });
            });
        }
    }
}
