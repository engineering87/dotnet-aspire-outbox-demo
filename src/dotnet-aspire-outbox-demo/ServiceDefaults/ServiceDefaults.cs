// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace ServiceDefaults
{
    public static class ServiceDefaultsExtensions
    {
        public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
        {
            // Health checks
            builder.Services.AddHealthChecks();

            // Required for endpoint metadata generation (used by Swagger)
            builder.Services.AddEndpointsApiExplorer();

            // Swagger (Swashbuckle)
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = builder.Environment.ApplicationName,
                    Version = "v1"
                });
            });

            return builder;
        }

        public static WebApplication UseServiceDefaults(this WebApplication app)
        {
            // Shared health endpoint
            app.MapHealthChecks("/health");

            // Swagger UI (typically only enabled in Development)
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json",
                        $"{app.Environment.ApplicationName} v1");
                });
            }

            return app;
        }
    }
}