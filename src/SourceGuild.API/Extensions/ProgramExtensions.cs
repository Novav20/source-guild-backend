using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SourceGuild.Infrastructure.Persistence;
using SourceGuild.Infrastructure.Persistence.Seed;
using System.Reflection;
using SourceGuild.Domain.Entities;

namespace SourceGuild.API.Extensions;

/// <summary>
/// Provides extension methods for configuring the application's services and pipeline.
/// </summary>
public static class ProgramExtensions
{
    /// <summary>
    /// Adds CORS configuration to the service collection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="configuration">The application's configuration.</param>
    /// <returns>The IServiceCollection for chaining.</returns>
    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        services.AddCors(options =>
        {
            options.AddPolicy(name: MyAllowSpecificOrigins, policyBuilder =>
            {
                var allowedOrigins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
                if (allowedOrigins != null && allowedOrigins.Length > 0)
                {
                    policyBuilder.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });
        return services;
    }

    /// <summary>
    /// Adds Swagger/OpenAPI configuration to the service collection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <returns>The IServiceCollection for chaining.</returns>
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });
        return services;
    }

    /// <summary>
    /// Configures the application's HTTP request pipeline.
    /// </summary>
    /// <param name="app">The WebApplication instance.</param>
    /// <returns>The WebApplication for chaining.</returns>
    public static WebApplication ConfigureApplicationPipeline(this WebApplication app)
    {
        var MyAllowSpecificOrigins = "_myAllowSpecificOrigins"; // Need to define it here again or pass it

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHttpsRedirection();
        }
        app.UseCors(MyAllowSpecificOrigins);

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        return app;
    }

    /// <summary>
    /// Configures the database and performs data seeding at application startup.
    /// </summary>
    /// <param name="app">The WebApplication instance.</param>
    /// <returns>A Task representing the asynchronous operation, returning the WebApplication for chaining.</returns>
    public static async Task<WebApplication> ConfigureDatabaseAndSeeding(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var serviceProvider = scope.ServiceProvider;
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            var context = serviceProvider.GetRequiredService<SGDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            // Apply migrations to create/update database schema
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");

            // Seed essential data (roles, admin user)
            await EssentialDataSeeder.SeedDataAsync(serviceProvider);
            logger.LogInformation("Essential data seeded successfully.");

            // Seed test data only in Development environment
            if (app.Environment.IsDevelopment())
            {
                await TestDataSeeder.SeedDataAsync(context, userManager);
                logger.LogInformation("Test data seeded successfully.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during the seeding process.");
        }

        return app;
    }
}